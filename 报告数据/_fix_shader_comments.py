import io, re

p = r"D:\HE\unity\Git\GITUHUB\vivi\Assets\Shaders\SpineSilhouette.shader"
lines = io.open(p, encoding="utf-8").read().split("\n")

clean_header = """// Vivi/SpineSilhouette
// 残影用的"纯色剪影"Shader：只取 Spine 图集贴图的 alpha 来抠形状，RGB 输出 _Color 的纯色。
// 注意：不能完全不采样贴图 —— Spine 图集里每个区域都是矩形，不采样 alpha 会画出一块块方块。
// 使用方式见 PlayerEvade（运行时按图集贴图建材质，不需要手动建 .mat）。
//
// _Flat = 1：硬边剪影（alpha 低于 _Cutoff 的丢弃，其余像素输出一致的不透明度），
//            配合模板缓冲，同一张残影里身体部件叠在一起的地方不会颜色变深，整体就是一块纯色。
// _Flat = 0：软边（保留图集 alpha），部件叠加处会更实。
//
// 渲染管线：没有写 LightMode 标签，走 URP 2D Renderer 的 SRPDefaultUnlit 通道（与 Spine 自带的 Spine/Skeleton 一样）。""".split("\n")

# 找到 Shader "..." 行，之前的注释全部替换成干净版本
idx = next(i for i, l in enumerate(lines) if l.strip().startswith('Shader "'))
out = clean_header + lines[idx:]
io.open(p, "w", encoding="utf-8", newline="\r\n").write("\n".join(out))
print("comments rewritten; lines:", len(out))
