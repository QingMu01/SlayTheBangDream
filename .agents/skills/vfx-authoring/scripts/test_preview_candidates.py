"""预演工具的像素行为、来源错误与形态判定回归验证。"""
from __future__ import annotations

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image, ImageDraw
import preview_candidates as preview


class PreviewTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def source(self, name, mode="RGBA", color=(100, 80, 60, 128), size=(8, 8)):
        path = self.root / name
        Image.new(mode, size, color).save(path)
        return str(path)

    def item(self, path, **values):
        return {"path": path, "label": "fixture", "role": "ring", "blend": "alpha",
                "display": 8, **values}

    def render(self, item):
        entry = preview.prepare(item)
        return preview.composite_layer(Image.new("RGB", (8, 8), (50, 50, 50)), entry, (4, 4))

    def test_mix_and_add_use_texture_alpha(self):
        path = self.source("rgba.png")
        self.assertEqual(self.render(self.item(path)).getpixel((4, 4)), (75, 65, 55))
        self.assertEqual(self.render(self.item(path, blend="alpha_add")).getpixel((4, 4)), (100, 90, 80))

    def test_zero_alpha_keeps_background_in_both_modes(self):
        path = self.source("transparent.png", color=(255, 255, 255, 0))
        for blend in ("alpha", "alpha_add"):
            self.assertEqual(self.render(self.item(path, blend=blend)).getpixel((4, 4)), (50, 50, 50))

    def test_tint_and_node_alpha_affect_add(self):
        path = self.source("white.png", color=(255, 255, 255, 255))
        image = self.render(self.item(path, blend="alpha_add", tint="#FF0000", alpha=0.5))
        self.assertEqual(image.getpixel((4, 4)), (178, 50, 50))

    def test_intensity_is_applied_before_additive_clipping(self):
        path = self.source("white_hdr.png", color=(255, 255, 255, 255))
        image = self.render(self.item(path, blend="alpha_add", intensity=2.0, alpha=0.25))
        self.assertEqual(image.getpixel((4, 4)), (178, 178, 178))

    def test_add_saturates(self):
        path = self.source("bright.png", color=(255, 255, 255, 255))
        self.assertEqual(self.render(self.item(path, blend="alpha_add")).getpixel((4, 4)), (255, 255, 255))

    def test_crush_removes_black_and_preserves_visible_shape(self):
        path = self.root / "blackbase.png"
        image = Image.new("RGB", (8, 8), (10, 10, 10))
        ImageDraw.Draw(image).rectangle((2, 2, 5, 5), fill=(128, 128, 128))
        image.save(path)
        item = self.item(str(path), blend="crush_add", black_point=0.1, softness=0.1)
        entry = preview.prepare(item)
        result = self.render(item)
        self.assertEqual(result.getpixel((0, 0)), (50, 50, 50))
        self.assertEqual(result.getpixel((4, 4)), (178, 178, 178))
        self.assertAlmostEqual(entry["stats"]["coverage"], 0.25)

    def test_crush_soft_transition_and_modulation(self):
        path = self.source("gray.png", mode="RGB", color=(64, 64, 64))
        image = self.render(self.item(path, blend="crush_add", black_point=0.0,
                           softness=0.5, tint="#FF0000", alpha=0.5, intensity=2))
        self.assertEqual(image.getpixel((4, 4)), (82, 50, 50))

    def test_crush_rejects_texture_alpha_and_bad_thresholds(self):
        rgba = self.source("alpha.png")
        with self.assertRaises(ValueError):
            preview.prepare(self.item(rgba, blend="crush_add", black_point=0.1, softness=0.1))
        rgb = self.source("opaque.png", mode="RGB", color="white")
        for black, soft in ((0.1, 0), (0.8, 0.4), (-0.1, 0.1)):
            with self.assertRaises(ValueError):
                preview.prepare(self.item(rgb, blend="crush_add", black_point=black, softness=soft))

    def test_hole_exception_requires_reason(self):
        path = self.root / "ring.png"
        image = Image.new("RGBA", (8, 8))
        ImageDraw.Draw(image).rectangle((1, 1, 6, 6), outline="white", width=1)
        image.save(path)
        item = self.item(str(path), role="burst")
        self.assertTrue(preview.prepare(item)["problems"])
        explained = preview.prepare({**item, "veto_reason": "有意保留环形主体"})
        self.assertFalse(explained["problems"])
        self.assertTrue(explained["warnings"])

    def test_soft_glow_is_not_rejected_as_transparent(self):
        # α=80 的柔光板：按契约口径（CONTENT_ALPHA=32，与核心 ALPHA_TRANSPARENT 一致）
        # 它是**看得见的内容**，因此既不能否决，也不该报"主体低于阈值"。
        path = self.source("soft.png", color=(255, 255, 255, 80))
        entry = preview.prepare(self.item(path, role="glow"))
        self.assertFalse(entry["problems"])
        self.assertFalse(entry["stats"].get("empty", False))
        self.assertFalse(
            [w for w in entry["warnings"] if "分析阈值" in w],
            "α=80 ≥ CONTENT_ALPHA(32)，不应被判成低于阈值",
        )
        # 真正看不见的（α=8 < 32）才该提示"降低 silhouette_threshold 或目视"
        faint = self.source("faint.png", color=(255, 255, 255, 8))
        entry = preview.prepare(self.item(faint, role="glow"))
        self.assertTrue(
            [w for w in entry["warnings"] if "分析阈值" in w],
            "α=8 < CONTENT_ALPHA(32) 应提示阈值过低",
        )

    def test_content_size_survives_analysis_downsampling(self):
        stats = preview.analyze(Image.new("L", (512, 256), 255))
        self.assertEqual(stats["content_px"], (512, 256))

    def test_contract_constants_match_polaris_core(self):
        """预演脚本与 Polaris 核心的共用口径**必须逐项一致**。

        对照表见 references/selection-contract.md；权威来源是
        `get_selection_spec`（核心注册表生成）。这里把数值钉死在测试里，
        任何一边改动而另一边没跟上都会在这里失败——
        跨仓库的漂移靠这个测试 + PolarisManager 的
        `scripts/baseline/check_skill_alignment.py` 双向兜住。
        """
        self.assertEqual(preview.CONTENT_ALPHA, 32, "应与核心 ALPHA_TRANSPARENT 一致")
        self.assertEqual(preview.ANALYSIS_MAX, 256, "应与核心 WORK_SIZE 一致")
        self.assertEqual(preview.MAX_UPSCALE, 3.0, "应与核心 selection::MAX_UPSCALE 一致")
        self.assertEqual(
            sorted(preview.VETO),
            ["burst", "debris", "flash", "glow", "ring", "smoke", "spark", "trail"],
            "角色集合应与核心 selection::ROLES 一致（8 个，不含弃用的 atlas）",
        )
        self.assertEqual(preview.DEPRECATED_ROLES, {"atlas": "spark"})

    def test_deprecated_atlas_role_still_runs_with_warning(self):
        path = self.source("page.png", color=(255, 255, 255, 255))
        entry = preview.prepare(self.item(path, role="atlas"))
        self.assertTrue(
            [w for w in entry["warnings"] if "已弃用" in w],
            "role=atlas 应给出弃用告警并继续",
        )

    def cli(self, items, **spec_values):
        spec = self.root / "spec.json"
        spec.write_text(json.dumps({"items": items, **spec_values}), encoding="utf-8")
        out = self.root / "preview.png"
        with patch("sys.argv", ["preview", "--spec", str(spec), "--out", str(out)]):
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                return preview.main(), out

    def test_cli_composition_respects_z_order(self):
        red = self.source("red.png", color=(255, 0, 0, 255))
        blue = self.source("blue.png", color=(0, 0, 255, 255))
        code, out = self.cli([self.item(blue, z=10), self.item(red, z=0)],
                             composition={"width": 8, "height": 8})
        self.assertEqual(code, 0)
        with Image.open(out.with_name("preview_composite.png")) as result:
            self.assertEqual(result.getpixel((4, 4)), (0, 0, 255))
        self.assertTrue(out.is_file())

    def test_cli_invalid_spec_and_unresolved_veto_exit_codes(self):
        code, _ = self.cli([self.item(str(self.root / "missing.png"))])
        self.assertEqual(code, 3)
        path = self.source("particle.png", color="white")
        code, _ = self.cli([self.item(path, role="spark", display=32)])
        self.assertEqual(code, 4)
        code, _ = self.cli([self.item(path, blend="unknown")])
        self.assertEqual(code, 3)


if __name__ == "__main__":
    unittest.main()
