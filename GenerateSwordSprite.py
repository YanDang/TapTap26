from PIL import Image, ImageDraw

def generate_sword():
    w, h = 64, 64
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 45 degree angle greatsword with fiery lava glow
    # Hilt at (14, 50), Tip at (52, 12)
    # Pommel
    draw.ellipse([10, 52, 16, 58], fill=(120, 120, 130, 255), outline=(70, 70, 80, 255))
    # Grip
    draw.line([(13, 53), (20, 46)], fill=(80, 50, 30, 255), width=3)
    # Crossguard
    draw.line([(15, 41), (25, 51)], fill=(180, 150, 60, 255), width=4)
    draw.ellipse([13, 39, 17, 43], fill=(220, 180, 70, 255))
    draw.ellipse([23, 49, 27, 53], fill=(220, 180, 70, 255))

    # Blade spine
    draw.line([(20, 44), (52, 12)], fill=(220, 230, 240, 255), width=4)
    # Fiery edge / runic glow
    draw.line([(22, 42), (54, 10)], fill=(255, 120, 20, 220), width=2)
    draw.line([(18, 46), (50, 14)], fill=(255, 60, 0, 200), width=2)
    # Bright center core
    draw.line([(23, 41), (49, 15)], fill=(255, 240, 160, 255), width=1)
    # Tip point
    draw.polygon([(52, 12), (56, 8), (53, 15)], fill=(255, 250, 220, 255))

    img.save(r"D:\Game_Develop\TapTap26\Assets\Art\Sprites\fire_greatsword.png")
    print("Saved fire_greatsword.png")

generate_sword()
