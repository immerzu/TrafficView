from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parent
PROJECT_ROOT = ROOT.parents[1]
COMFY_OUTPUT = Path(r"D:\Codex\!Grafikhilfen\ComfyUI\ComfyUI\output\comfy04_widget_rebuild_00001_.png")
FALLBACK_SOURCE = PROJECT_ROOT / "Skin_Work" / "Reference_Inputs" / "Futuristisches Widget mit Geschwindigkeitsanzeige.png"

BASE_WIDTH = 102
BASE_HEIGHT = 56
BASE_METER_BOUNDS = (63, 7, 30, 30)

TARGETS = [
    ("TrafficView.panel.90.png", 92, 50),
    ("TrafficView.panel.png", 102, 56),
    ("TrafficView.panel.110.png", 112, 62),
    ("TrafficView.panel.125.png", 128, 70),
    ("TrafficView.panel.150.png", 153, 84),
]


def new_layer(size: tuple[int, int]) -> Image.Image:
    return Image.new("RGBA", size, (0, 0, 0, 0))


def get_source_path() -> Path:
    if COMFY_OUTPUT.exists():
        return COMFY_OUTPUT
    return FALLBACK_SOURCE


def crop_source_widget(source: Image.Image) -> Image.Image:
    width, height = source.size
    pixels = source.load()
    min_x, min_y = width, height
    max_x = max_y = -1

    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a > 0 and not (r > 246 and g > 246 and b > 246):
                min_x = min(min_x, x)
                min_y = min(min_y, y)
                max_x = max(max_x, x)
                max_y = max(max_y, y)

    if max_x < min_x or max_y < min_y:
        return source

    pad_x = max(6, int(round((max_x - min_x + 1) * 0.01)))
    pad_y = max(6, int(round((max_y - min_y + 1) * 0.01)))
    return source.crop(
        (
            max(0, min_x - pad_x),
            max(0, min_y - pad_y),
            min(width, max_x + pad_x + 1),
            min(height, max_y + pad_y + 1),
        )
    )


def scaled_meter_geometry(width: int, height: int) -> tuple[float, float, float]:
    scale_x = width / float(BASE_WIDTH)
    scale_y = height / float(BASE_HEIGHT)
    meter_x, meter_y, meter_w, meter_h = BASE_METER_BOUNDS
    left = meter_x * scale_x
    top = meter_y * scale_y
    w = meter_w * scale_x
    h = meter_h * scale_y
    center_x = left + (w / 2.0)
    center_y = top + (h / 2.0)
    radius = min(w, h) / 2.0
    return center_x, center_y, radius


def clear_left_value_zone(image: Image.Image) -> Image.Image:
    width, height = image.size

    layer = new_layer(image.size)
    draw = ImageDraw.Draw(layer)

    panel_box = (
        int(round(width * 0.07)),
        int(round(height * 0.12)),
        int(round(width * 0.60)),
        int(round(height * 0.83)),
    )
    draw.rounded_rectangle(
        panel_box,
        radius=max(4, int(round(height * 0.14))),
        fill=(5, 16, 29, 158),
    )

    top_box = (
        int(round(width * 0.09)),
        int(round(height * 0.14)),
        int(round(width * 0.57)),
        int(round(height * 0.39)),
    )
    bottom_box = (
        int(round(width * 0.09)),
        int(round(height * 0.46)),
        int(round(width * 0.57)),
        int(round(height * 0.72)),
    )

    for box in (top_box, bottom_box):
        draw.rounded_rectangle(
            box,
            radius=max(3, int(round(height * 0.07))),
            fill=(4, 14, 28, 252),
        )

    image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(max(1, int(round(width * 0.012))))))

    edge = new_layer(image.size)
    edraw = ImageDraw.Draw(edge)
    for box in (top_box, bottom_box):
        edraw.rounded_rectangle(
            box,
            radius=max(3, int(round(height * 0.07))),
            outline=(118, 230, 250, 38),
            width=1,
        )
    image.alpha_composite(edge.filter(ImageFilter.GaussianBlur(1)))
    return image


def clear_right_circle_zone(image: Image.Image) -> Image.Image:
    width, height = image.size
    cx, cy, live_radius = scaled_meter_geometry(width, height)

    # Build the skin carrier concentrically around the exact live-ring center.
    outer_r = live_radius + (height * 0.105)
    mid_r = live_radius + (height * 0.03)
    inner_r = max(1.0, live_radius - (height * 0.035))

    clear_layer = new_layer(image.size)
    cdraw = ImageDraw.Draw(clear_layer)
    cdraw.ellipse(
        (
            cx - outer_r - (height * 0.20),
            cy - outer_r - (height * 0.20),
            cx + outer_r + (height * 0.20),
            cy + outer_r + (height * 0.20),
        ),
        fill=(6, 16, 30, 232),
    )
    cdraw.rounded_rectangle(
        (
            cx + (width * 0.10),
            cy + (height * 0.07),
            width - 1,
            height - 2,
        ),
        radius=max(2, int(round(height * 0.06))),
        fill=(8, 24, 40, 150),
    )
    image.alpha_composite(clear_layer.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.022))))))

    beam = new_layer(image.size)
    bdraw = ImageDraw.Draw(beam)
    beam_h = max(3, int(round(height * 0.10)))
    bdraw.rounded_rectangle(
        (
            cx - outer_r - (width * 0.18),
            cy - beam_h / 2.0,
            min(width - 4.0, cx + outer_r + (width * 0.13)),
            cy + beam_h / 2.0,
        ),
        radius=max(2, beam_h // 2),
        fill=(115, 240, 255, 32),
    )
    image.alpha_composite(beam.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.02))))))

    ring = new_layer(image.size)
    rdraw = ImageDraw.Draw(ring)
    outer_box = (cx - outer_r, cy - outer_r, cx + outer_r, cy + outer_r)
    mid_box = (cx - mid_r, cy - mid_r, cx + mid_r, cy + mid_r)
    inner_box = (cx - inner_r, cy - inner_r, cx + inner_r, cy + inner_r)

    rdraw.ellipse(
        outer_box,
        outline=(38, 190, 255, 118),
        width=max(2, int(round(width * 0.015))),
    )
    rdraw.ellipse(
        mid_box,
        outline=(56, 128, 255, 94),
        width=max(1, int(round(width * 0.012))),
    )
    rdraw.ellipse(
        inner_box,
        outline=(102, 240, 255, 98),
        width=max(1, int(round(width * 0.01))),
    )

    image.alpha_composite(ring.filter(ImageFilter.GaussianBlur(max(1, int(round(width * 0.012))))))
    image.alpha_composite(ring)
    return image


def reinforce_gloss(image: Image.Image) -> Image.Image:
    width, height = image.size
    layer = new_layer(image.size)
    draw = ImageDraw.Draw(layer)
    draw.rounded_rectangle(
        (4, 2, width - 5, int(round(height * 0.27))),
        radius=max(3, int(round(height * 0.11))),
        fill=(255, 255, 255, 16),
    )
    draw.ellipse(
        (int(round(width * 0.84)), int(round(height * 0.67)), width + 4, height + 3),
        fill=(210, 255, 255, 48),
    )
    image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(max(2, int(round(width * 0.02))))))
    return image


def build_asset(width: int, height: int) -> Image.Image:
    source = Image.open(get_source_path()).convert("RGBA")
    cropped = crop_source_widget(source)
    image = cropped.resize((width, height), Image.Resampling.LANCZOS)
    image = clear_left_value_zone(image)
    image = clear_right_circle_zone(image)
    image = reinforce_gloss(image)
    image = image.filter(ImageFilter.UnsharpMask(radius=1.1, percent=140, threshold=2))
    return image


def main() -> None:
    source_path = get_source_path()
    if not source_path.exists():
        raise FileNotFoundError(f"Source image not found: {source_path}")

    for filename, width, height in TARGETS:
        output = build_asset(width, height)
        output.save(ROOT / filename)
        print(ROOT / filename)


if __name__ == "__main__":
    main()
