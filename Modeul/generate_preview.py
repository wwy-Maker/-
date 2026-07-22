"""
生成美术资产总览预览图 — 把所有Sprite拼到一张图上
"""
from PIL import Image, ImageDraw, ImageFont
import os

OUTPUT_BASE = r"D:\诸子百家_口诛笔伐\Modeul"

def load_png(category, name):
    path = os.path.join(OUTPUT_BASE, category, f"{name}.png")
    if os.path.exists(path):
        return Image.open(path).convert("RGBA")
    return None

# 画布: 1600 x 2400
canvas = Image.new("RGBA", (1600, 2400), (30, 30, 35, 255))
draw = ImageDraw.Draw(canvas)

# 标题
try:
    font_large = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 32)
    font_med = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 20)
    font_small = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 14)
except:
    font_large = ImageFont.load_default()
    font_med = ImageFont.load_default()
    font_small = ImageFont.load_default()

draw.text((560, 20), "诸子百家·口诛笔伐 — 灰模美术资产总览", fill=(255,255,255,255), font=font_large)
draw.text((600, 60), "Demo Prototype v0.1 | 50项Sprite + Shader代码", fill=(180,180,180,255), font=font_med)

y = 100

def draw_section(title, y):
    draw.text((40, y), title, fill=(100, 200, 255, 255), font=font_med)
    draw.line([(40, y+28), (1560, y+28)], fill=(60,60,70,255), width=1)
    return y + 40

def draw_sprite_with_label(img, x, y, label, scale=1):
    if img is None:
        return
    w, h = img.size
    if scale != 1:
        img = img.resize((int(w*scale), int(h*scale)), Image.NEAREST)
        w, h = img.size
    # 居中放置在120px高度的行内
    offset_x = max(0, (120 - w) // 2)
    offset_y = max(0, (60 - h) // 2)
    canvas.paste(img, (x + offset_x, y + offset_y), img)
    draw.text((x, y + 65), label, fill=(200,200,200,255), font=font_small)

# === 1. 玩家角色 ===
y = draw_section("玩家角色 (圆形 = 玩家身份, 描边色 = 学派身份)", y)
items = [
    ("玩家角色", "player_base"),
    ("玩家角色", "player_ring_confucian"),
    ("玩家角色", "player_ring_legalist"),
    ("玩家角色", "player_ring_daoist"),
]
labels = ["素白基底(48px)", "儒家描边(金)", "法家描边(黑)", "道家描边(青)"]
for i, (cat, name) in enumerate(items):
    img = load_png(cat, name)
    draw_sprite_with_label(img, 40 + i*380, y, labels[i], scale=2)
y += 110

# === 2. 弟子 ===
y = draw_section("弟子 (方形 = 敌人, 填色 = 学派, 描边 = 精英)", y)
disciples = [
    ("弟子", "disciple_confucian_normal", "儒家普通(36px)"),
    ("弟子", "disciple_confucian_elite", "儒家精英(44px)"),
    ("弟子", "disciple_legalist_normal", "法家普通(36px)"),
    ("弟子", "disciple_legalist_elite", "法家精英(44px)"),
    ("弟子", "disciple_daoist_normal", "道家普通(36px)"),
    ("弟子", "disciple_daoist_elite", "道家精英(44px)"),
]
for i, (cat, name, label) in enumerate(disciples):
    img = load_png(cat, name)
    col = i % 3
    row = i // 3
    draw_sprite_with_label(img, 40 + col*500, y + row*110, label, scale=2)
y += 110 * 2 + 20

# === 3. Boss ===
y = draw_section("Boss (六边形, 3阶段尺寸+亮度递增)", y)
bosses = [
    ("Boss", "boss_confucian_phase1", "儒宗师·入世"),
    ("Boss", "boss_confucian_phase2", "儒宗师·仁义"),
    ("Boss", "boss_confucian_phase3", "儒宗师·大同"),
    ("Boss", "boss_legalist_phase1", "法宗师·明法"),
    ("Boss", "boss_legalist_phase2", "法宗师·严刑"),
    ("Boss", "boss_legalist_phase3", "法宗师·极刑"),
    ("Boss", "boss_daoist_phase1", "道宗师·无为"),
    ("Boss", "boss_daoist_phase2", "道宗师·逍遥"),
    ("Boss", "boss_daoist_phase3", "道宗师·天道"),
]
for i, (cat, name, label) in enumerate(bosses):
    img = load_png(cat, name)
    col = i % 3
    row = i // 3
    draw_sprite_with_label(img, 40 + col*500, y + row*120, label, scale=1.5)
y += 120 * 3 + 20

# === 4. 弹幕 — 玩家 ===
y = draw_section("玩家弹幕 (素白色, 3种主武器)", y)
player_bullets = [
    ("弹幕", "bullet_archery_arrow", "射艺箭矢(24x6)", 3),
    ("弹幕", "bullet_archery_charge", "射艺蓄力箭(32x8)", 3),
    ("弹幕", "bullet_yu_dash_trail", "御艺冲刺带", 2),
    ("弹幕", "bullet_li_push_wave", "礼击推力波(扇形)", 1.5),
    ("弹幕", "bullet_li_barrier", "礼屏障(空心圆)", 1.5),
    ("弹幕", "bullet_li_reflect_circle", "礼反弹圈", 1.5),
]
for i, (cat, name, label, sc) in enumerate(player_bullets):
    img = load_png(cat, name)
    col = i % 3
    row = i // 3
    draw_sprite_with_label(img, 40 + col*500, y + row*120, label, scale=sc)
y += 120 * 2 + 20

# === 5. 弹幕 — 敌人/Boss ===
y = draw_section("敌人/Boss弹幕 (学派色, 形状编码=灰阶可辨硬约束)", y)
enemy_bullets = [
    ("弹幕", "bullet_confucian_spread", "儒家扩散弹(圆)", 3),
    ("弹幕", "bullet_legalist_line", "法家直线弹(三角)", 3),
    ("弹幕", "bullet_daoist_arc", "道家弧线弹(月牙)", 3),
    ("弹幕", "bullet_boss_confucian_spread", "Boss儒家弹(大圆)", 3),
    ("弹幕", "bullet_boss_legalist_track", "Boss法家追踪(大三角)", 3),
    ("弹幕", "bullet_boss_daoist_ripple", "Boss道宗师波纹(半透明)", 2),
]
for i, (cat, name, label, sc) in enumerate(enemy_bullets):
    img = load_png(cat, name)
    col = i % 3
    row = i // 3
    draw_sprite_with_label(img, 40 + col*500, y + row*120, label, scale=sc)
y += 120 * 2 + 20

# === 6. 场地与拾取物 ===
y = draw_section("场地与拾取物", y)
arena_items = [
    ("场地与拾取物", "arena_ground_tile", "场地tile(灰)"),
    ("场地与拾取物", "arena_water_tile", "浅水tile(青灰)"),
    ("场地与拾取物", "arena_cyclone_zone", "气旋阵(虚线圆)"),
    ("场地与拾取物", "pickup_knowledge", "学识掉落(白点)"),
]
for i, (cat, name, label) in enumerate(arena_items):
    img = load_png(cat, name)
    draw_sprite_with_label(img, 40 + i*380, y, label, scale=2)
y += 110

# === 7. HUD ===
y = draw_section("HUD元素", y)
hud_items = [
    ("HUD元素", "hud_hp_bar_bg", "HP背景"),
    ("HUD元素", "hud_hp_bar_fill_green", "HP填充(绿)"),
    ("HUD元素", "hud_hp_bar_fill_yellow", "HP填充(黄)"),
    ("HUD元素", "hud_hp_bar_fill_red", "HP填充(红)"),
    ("HUD元素", "hud_stamina_bar_bg", "体力背景"),
    ("HUD元素", "hud_stamina_bar_fill", "体力填充(蓝)"),
    ("HUD元素", "hud_knowledge_icon", "学识图标"),
    ("HUD元素", "hud_wave_indicator", "波次图标"),
    ("HUD元素", "hud_boss_phase_icon", "Boss阶段图标"),
]
for i, (cat, name, label) in enumerate(hud_items):
    img = load_png(cat, name)
    col = i % 3
    row = i // 3
    draw_sprite_with_label(img, 40 + col*500, y + row*100, label, scale=2)
y += 100 * 3 + 20

# 底部信息
y += 20
draw.line([(40, y), (1560, y)], fill=(60,60,70,255), width=1)
y += 10
draw.text((40, y), "颜色编码: 儒金#D4A017 | 法黑#1A1A1A | 道青#2E8B8B | 素白#FFFFFF", fill=(150,150,150,255), font=font_small)
y += 25
draw.text((40, y), "灰阶测试: 按G键切换灰阶模式 | 灰阶公式: L = 0.299R + 0.587G + 0.114B", fill=(150,150,150,255), font=font_small)
y += 25
draw.text((40, y), "形状编码: 圆形=玩家 | 方形=弟子 | 六边形=Boss | 三角=法弹 | 月牙=道弹 | 圆=儒弹", fill=(150,150,150,255), font=font_small)

# 保存
output_path = os.path.join(OUTPUT_BASE, "美术资产总览预览.png")
canvas.save(output_path, "PNG")
print(f"总览图已保存: {output_path}")
print(f"尺寸: {canvas.size}")
