"""生成 EliCloud 客户端里两个「矢量图标类」资源。

设计稿本身没有画产品 Logo，也没有画 PDF 服务的图标，所以这两个位图由脚本生成：
  - elicloud-app-icon-cloud.png  ：标题栏左侧的产品图标（Segoe Fluent Icons 的云朵字形）
  - service-icon-pdf-lock.png    ：PDF 在线解密服务的卡片/Nav 图标（文档 + 锁）

其余资源都是从 .fig 里 1:1 导出的原图，见 Assets/Images/README.md。
"""

from PIL import Image, ImageDraw, ImageFont

FLUENT_FONTS = [
    r"C:\Windows\Fonts\SegoeIcons.ttf",   # Segoe Fluent Icons (Windows 11)
    r"C:\Windows\Fonts\segmdl2.ttf",      # Segoe MDL2 Assets (回退)
]

GLYPH_CLOUD = "\uE753"
GLYPH_DOCUMENT = "\uE8A5"
GLYPH_LOCK = "\uE72E"

ACCENT = (0, 95, 184, 255)        # WinUI Light Accent/Default #005FB8
PDF_MAIN = (0, 95, 184, 255)
PDF_LOCK = (196, 43, 28, 255)     # SystemCritical 一类的红，表示「加密」

OUT_DIR = r"D:\work\projects\EliCloud\EliCloudDesktop\src\EliCloudDesktop\Assets\Images"


def load_font(size: int) -> ImageFont.FreeTypeFont:
    for path in FLUENT_FONTS:
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    raise SystemExit("找不到 Segoe Fluent Icons / Segoe MDL2 Assets 字体")


def render_glyph(glyph: str, size: int, color, canvas_size: int) -> Image.Image:
    """把一个图标字形居中渲染到透明画布上。"""
    scale = 4  # 先放大再缩小，拿到抗锯齿边缘
    font = load_font(size * scale)
    image = Image.new("RGBA", (canvas_size * scale, canvas_size * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    box = draw.textbbox((0, 0), glyph, font=font)
    draw.text(
        (
            (canvas_size * scale - (box[2] - box[0])) / 2 - box[0],
            (canvas_size * scale - (box[3] - box[1])) / 2 - box[1],
        ),
        glyph,
        font=font,
        fill=color,
    )
    return image.resize((canvas_size, canvas_size), Image.LANCZOS)


def draw_padlock(image: Image.Image, origin: tuple[int, int], size: int, color) -> None:
    """在 (origin) 处画一个 size×size 的挂锁（锁体 + 锁梁）。

    Segoe Fluent Icons 里对应锁的字形在不同 Windows 版本上位置不一致，
    所以这里用基本图元画，保证任何机器上生成的资源都一样。
    """
    scale = 4
    big = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(big)
    body_h = int(size * scale * 0.52)
    body_w = size * scale
    body_top = size * scale - body_h
    radius = max(1, int(size * scale * 0.14))
    draw.rounded_rectangle((0, body_top, body_w - 1, size * scale - 1), radius=radius, fill=color)

    # 锁梁：一段圆环
    shackle_w = int(size * scale * 0.56)
    shackle_h = int(size * scale * 0.46)
    left = (body_w - shackle_w) // 2
    draw.arc(
        (left, 0, left + shackle_w, shackle_h),
        start=180,
        end=360,
        fill=color,
        width=max(2, int(size * scale * 0.13)),
    )
    big = big.resize((size, size), Image.LANCZOS)
    image.alpha_composite(big, origin)


def main() -> None:
    # 产品图标：256×256，深色/浅色任务栏都能看清的强调色云朵
    cloud = render_glyph(GLYPH_CLOUD, 200, ACCENT, 256)
    cloud.save(f"{OUT_DIR}\\elicloud-app-icon-cloud.png")

    # PDF 服务图标：64×64，文档字形 + 右下角挂锁
    doc = render_glyph(GLYPH_DOCUMENT, 48, PDF_MAIN, 64)
    draw_padlock(doc, (34, 34), 28, PDF_LOCK)
    doc.save(f"{OUT_DIR}\\service-icon-pdf-lock.png")

    print("generated: elicloud-app-icon-cloud.png, service-icon-pdf-lock.png")


if __name__ == "__main__":
    main()
