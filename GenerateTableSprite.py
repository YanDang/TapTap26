import math
from PIL import Image, ImageDraw

def generate_furniture_sprite():
    # Isometric Heavy Table (64x64 or 96x96 pixels)
    width = 96
    height = 96
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Center of isometric diamond top
    cx, cy = 48, 42
    rx, ry = 36, 18  # Diamond radius

    # Table legs (4 legs)
    # Leg height = 24
    leg_h = 22
    leg_w = 7
    leg_color = (65, 45, 32, 255)
    leg_shadow = (45, 30, 22, 255)
    leg_highlight = (90, 65, 48, 255)

    # 4 corner positions of the diamond
    corners = [
        (cx, cy - ry),       # Top
        (cx + rx, cy),       # Right
        (cx, cy + ry),       # Bottom
        (cx - rx, cy)        # Left
    ]

    # Draw legs for left, right, bottom (top leg is hidden behind)
    leg_bases = [
        (cx - rx + 10, cy + 4),       # Left leg
        (cx + rx - 10, cy + 4),       # Right leg
        (cx, cy + ry - 4),           # Bottom/Front leg
        (cx, cy - ry + 8)            # Back leg (partially visible)
    ]

    # Draw legs
    for lx, ly in leg_bases:
        # Leg rectangle
        draw.rectangle([lx - 3, ly, lx + 3, ly + leg_h], fill=leg_color)
        draw.line([(lx - 3, ly), (lx - 3, ly + leg_h)], fill=leg_shadow, width=1)
        draw.line([(lx + 3, ly), (lx + 3, ly + leg_h)], fill=leg_highlight, width=1)
        # Metal foot brace
        draw.rectangle([lx - 3, ly + leg_h - 4, lx + 3, ly + leg_h], fill=(120, 120, 130, 255))

    # Cross support beams between legs
    draw.line([(leg_bases[0][0], leg_bases[0][1] + 12), (leg_bases[2][0], leg_bases[2][1] + 12)], fill=(50, 35, 25, 255), width=3)
    draw.line([(leg_bases[1][0], leg_bases[1][1] + 12), (leg_bases[2][0], leg_bases[2][1] + 12)], fill=(55, 38, 28, 255), width=3)

    # Table thick edge/sides (3D bevel downwards)
    side_thickness = 10
    # Left front side
    poly_left = [
        (cx - rx, cy),
        (cx, cy + ry),
        (cx, cy + ry + side_thickness),
        (cx - rx, cy + side_thickness)
    ]
    draw.polygon(poly_left, fill=(80, 52, 36, 255))
    draw.line([(cx - rx, cy + side_thickness), (cx, cy + ry + side_thickness)], fill=(50, 32, 20, 255), width=2)

    # Right front side
    poly_right = [
        (cx, cy + ry),
        (cx + rx, cy),
        (cx + rx, cy + side_thickness),
        (cx, cy + ry + side_thickness)
    ]
    draw.polygon(poly_right, fill=(110, 75, 52, 255))
    draw.line([(cx, cy + ry + side_thickness), (cx + rx, cy + side_thickness)], fill=(70, 45, 30, 255), width=2)

    # Corner metal braces
    draw.polygon([(cx - 4, cy + ry), (cx + 4, cy + ry), (cx + 4, cy + ry + side_thickness), (cx - 4, cy + ry + side_thickness)], fill=(160, 160, 175, 255))

    # Table top (Isometric Diamond)
    diamond_top = [
        (cx, cy - ry),       # Top
        (cx + rx, cy),       # Right
        (cx, cy + ry),       # Bottom
        (cx - rx, cy)        # Left
    ]
    draw.polygon(diamond_top, fill=(145, 102, 72, 255))

    # Wood planks / Granite slabs on top
    for t in [-0.5, 0.0, 0.5]:
        p1 = (cx + t * rx * 0.7 - 8, cy - ry * 0.7)
        p2 = (cx + t * rx * 0.7 + 8, cy + ry * 0.7)
        draw.line([p1, p2], fill=(120, 85, 60, 255), width=1)

    # Fiery Core / Rune in the center ("玄岩熔火" Lava Core Rune)
    # A glowing diamond rune in the center
    rcx, rcy = cx, cy
    rrx, rry = 12, 6
    draw.polygon([
        (rcx, rcy - rry),
        (rcx + rrx, rcy),
        (rcx, rcy + rry),
        (rcx - rrx, rcy)
    ], fill=(230, 80, 20, 240))

    # Inner glow
    draw.polygon([
        (rcx, rcy - rry + 2),
        (rcx + rrx - 4, rcy),
        (rcx, rcy + rry - 2),
        (rcx - rrx + 4, rcy)
    ], fill=(255, 190, 50, 255))

    # Edge highlight on the top rim
    draw.line([(cx - rx, cy), (cx, cy - ry), (cx + rx, cy)], fill=(185, 140, 105, 255), width=2)
    draw.line([(cx - rx, cy), (cx, cy + ry), (cx + rx, cy)], fill=(110, 75, 50, 255), width=1)

    out_path = r"D:\Game_Develop\TapTap26\Assets\Art\Sprites\heavy_table.png"
    img.save(out_path)
    print("Saved heavy_table.png successfully!")

generate_furniture_sprite()
