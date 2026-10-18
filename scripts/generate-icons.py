#!/usr/bin/env python3
"""从 AppIcon.svg 生成应用图标，保留项目原有画布尺寸。

用法：python scripts/generate-icons.py [--edge EDGE_PATH]
依赖：本地 Microsoft Edge 和 Pillow（python -m pip install Pillow）。
全程离线；Edge 渲染 SVG，Pillow 仅负责缩放、透明画布合成及 ICO 打包。
"""

from __future__ import annotations

import argparse
from io import BytesIO
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile
import xml.etree.ElementTree as ET

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "OtpClipboard" / "Assets"
ICO_SIZES = (16, 20, 24, 32, 48, 64, 128, 256)
# 原始资源的实际像素尺寸；不根据文件名推断，也不改变项目配置。
PNG_SIZES = {
    "LockScreenLogo.scale-200.png": (48, 48),
    "SplashScreen.scale-200.png": (1240, 600),
    "Square150x150Logo.scale-200.png": (300, 300),
    "Square44x44Logo.scale-200.png": (88, 88),
    "Square44x44Logo.targetsize-24_altform-unplated.png": (24, 24),
    "Square44x44Logo.targetsize-48_altform-lightunplated.png": (48, 48),
    "StoreLogo.png": (50, 50),
    "Wide310x150Logo.scale-200.png": (620, 300),
}


def find_edge(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit)
        if not path.is_file():
            raise FileNotFoundError(path)
        return path
    candidates = [shutil.which("msedge"), shutil.which("microsoft-edge")]
    for variable in ("PROGRAMFILES(X86)", "PROGRAMFILES", "LOCALAPPDATA"):
        directory = os.environ.get(variable)
        if directory:
            candidates.append(str(Path(directory) / "Microsoft/Edge/Application/msedge.exe"))
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return Path(candidate)
    raise RuntimeError("未找到本地 Edge，请用 --edge 指定路径。")


def render_svg(edge: Path, directory: Path) -> Image.Image:
    # 一次高分辨率渲染，所有导出均由同一 SVG 派生，避免双份绘图逻辑。
    size = 2048
    source = (ASSETS / "AppIcon.svg").read_text(encoding="utf-8")
    svg = ET.fromstring(source)
    svg.set("width", str(size))
    svg.set("height", str(size))
    ET.register_namespace("", "http://www.w3.org/2000/svg")
    html = (
        '<!doctype html><html><head><meta charset="utf-8">'
        '<style>html,body{margin:0;width:100%;height:100%;overflow:hidden;'
        'background:transparent}svg{display:block}</style></head><body>'
        + ET.tostring(svg, encoding="unicode") + '</body></html>'
    )
    page = directory / "icon.html"
    page.write_text(html, encoding="utf-8")
    screenshot = directory / "render.png"
    command = [
        str(edge), "--headless=new", "--disable-gpu", "--no-first-run",
        "--no-default-browser-check", "--disable-extensions",
        "--disable-background-networking", "--disable-sync", "--hide-scrollbars",
        "--force-device-scale-factor=1", "--default-background-color=00000000",
        f"--user-data-dir={directory / 'edge-profile'}",
        f"--window-size={size},{size}", f"--screenshot={screenshot}", page.as_uri(),
    ]
    result = subprocess.run(command, capture_output=True, timeout=90)
    if result.returncode or not screenshot.is_file():
        raise RuntimeError("Edge SVG 渲染失败：" + result.stderr.decode("utf-8", errors="replace")[-2000:])
    with Image.open(screenshot) as rendered:
        image = rendered.convert("RGBA")
    if image.size != (size, size) or image.getextrema()[3] != (0, 255):
        raise RuntimeError("SVG 渲染尺寸或透明通道不符合要求。")
    return image


def validate_png(path: Path, expected: tuple[int, int]) -> None:
    with Image.open(path) as image:
        if image.size != expected or image.mode != "RGBA":
            raise RuntimeError(f"PNG 尺寸/格式错误：{path.name}")
        if image.getextrema()[3] != (0, 255):
            raise RuntimeError(f"PNG 透明通道错误：{path.name}")
        for point in ((0, 0), (image.width - 1, 0), (0, image.height - 1),
                      (image.width - 1, image.height - 1)):
            if image.getpixel(point)[3] != 0:
                raise RuntimeError(f"PNG 画布角落不透明：{path.name}")


def validate_ico(path: Path) -> None:
    data = path.read_bytes()
    if struct.unpack_from("<HHH", data) != (0, 1, len(ICO_SIZES)):
        raise RuntimeError("ICO 文件头错误。")
    actual = []
    for index in range(len(ICO_SIZES)):
        width, height, _, _, planes, bits, length, offset = struct.unpack_from(
            "<BBBBHHII", data, 6 + index * 16
        )
        dimensions = (width or 256, height or 256)
        actual.append(dimensions)
        with Image.open(BytesIO(data[offset:offset + length])) as frame:
            if frame.size != dimensions or frame.mode != "RGBA" or frame.getextrema()[3] != (0, 255):
                raise RuntimeError(f"ICO 图层错误：{dimensions}")
        if (planes, bits) != (0, 32):
            raise RuntimeError("ICO 位深错误。")
    if actual != [(size, size) for size in ICO_SIZES]:
        raise RuntimeError(f"ICO 尺寸不完整：{actual}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--edge", help="本地 Edge 可执行文件路径")
    args = parser.parse_args()
    # 在覆盖任何资源之前确认现有尺寸，防止误改其他规格的资源。
    for name, dimensions in PNG_SIZES.items():
        target = ASSETS / name
        if target.exists():
            with Image.open(target) as existing:
                if existing.size != dimensions:
                    raise RuntimeError(f"现有尺寸已变化，请先更新 PNG_SIZES：{name} {existing.size}")
    with tempfile.TemporaryDirectory(prefix="otpclipboard-icons-", ignore_cleanup_errors=True) as scratch:
        directory = Path(scratch)
        master = render_svg(find_edge(args.edge), directory)
        for name, dimensions in PNG_SIZES.items():
            # 宽画布居中放置等比例图标，左右留透明区域，不拉伸手机。
            side = min(dimensions)
            icon = master.resize((side, side), Image.Resampling.LANCZOS)
            canvas = Image.new("RGBA", dimensions, (0, 0, 0, 0))
            canvas.alpha_composite(icon, ((dimensions[0] - side) // 2, (dimensions[1] - side) // 2))
            canvas.save(directory / name, optimize=True)
            validate_png(directory / name, dimensions)
        ico_path = directory / "AppIcon.ico"
        # 直接从高分辨率源图缩小，避免逐级缩放带来的细节损失。
        frames = [master.resize((size, size), Image.Resampling.LANCZOS) for size in ICO_SIZES]
        frames[-1].save(ico_path, format="ICO", sizes=[(s, s) for s in ICO_SIZES],
                        append_images=frames[:-1])
        validate_ico(ico_path)
        # 全部校验通过后才替换正式资源。
        for name in (*PNG_SIZES, "AppIcon.ico"):
            shutil.copyfile(directory / name, ASSETS / name)
            print(f"Generated: src/OtpClipboard/Assets/{name}")
    print("Validated: 8 PNG canvases; ICO 16/20/24/32/48/64/128/256; transparent RGBA.")


if __name__ == "__main__":
    main()
