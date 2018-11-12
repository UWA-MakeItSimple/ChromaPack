# ChromaPack

本项目是[keijiro/ChromaPack](https://github.com/keijiro/ChromaPack)的分支。
偶然在[UWA开源库](https://lab.uwa4d.com/)发现了这个项目，因为看到是K神的项目，且截图和dither算法纹理格式的用例相同，心想必有奇招，果然不失所望！

简单来说，ChromaPack（本文中简称为CP）是一个使用[Chroma Subsampling算法思路](https://en.wikipedia.org/wiki/Chroma_subsampling)的纹理压缩插件，可以使RGBA32/RBG24纹理转换成12bit（8bit*1.5）压缩格式，图像效果非常接近原图。

## 效果展示
![图片](https://github.com/uwaGOT/ChromaPack/blob/master/uwa-testimage/effect1.png)
[图1.四种效果的对比图]

![图片](https://github.com/uwaGOT/ChromaPack/blob/master/uwa-testimage/effect2.png)
[图2.为了让效果更明显的8倍放大图]

透明贴图对比（图1前两行）：
* 格式（从左至右）：RGBA 32bit，ChromaPack 12bit，ETC2 8bit，Dither4444 16bit；
* ChromaPack VS ETC2：可以看出在人物眼睛线条处，CP的表现优于ETC2，但CP的透明边缘有明显黑边；
* ChromaPack VS Dither：可以看出Dither在人物皮肤等纯色色块有明显噪点，但CP的透明边缘有明显黑边；

不透明贴图对比（图1后两行、图2）：
* 说明：由于图1中不透明图四种格式的表现相近，将测试图放大8倍进行测试得到图2；
* 格式：RGB 24bit，ChromaPack 12bit，ETC 4bit，Dither565 16bit；
* 效果对比：明显CP格式的效果优于ETC和Dither565；

## 实现方式
第一步：
参考示例工程中的ChromaPackProcesser.cs脚本，在OnPostprocessTexture中使用4:1:1Y'CrCb方案进行采样，并将原图保存成1.5倍宽、1倍高的Alpha8格式纹理，效果如下图：
![图片](https://github.com/uwaGOT/ChromaPack/blob/master/uwa-testimage/effect3.png)

需要注意的是，在这个步骤中若原图带有Alpha通道，计算亮度分量Y'时会占用一位表示透明度。不透明图和透明图的亮度分量计算公式如下：
```
//不透明图的亮度分量Y'计算公式
static float RGB_Y(Color rgb)
{
    return 0.299f * rgb.r + 0.587f * rgb.g + 0.114f * rgb.b;
}
    
//透明图的亮度分量Y'计算公式
static float RGB_Ya(Color rgb)
{
    if (rgb.a < 0.5f)
        return 0;
    else
        return RGB_Y(rgb) * 255 / 256 + 1.0f / 256;
}
```

第二步：
使用特定Shader（“ChromaPack / Opaque”和“ChromaPack / Cutout”）绘制图像，它可以动态解码并还原正确的图像。以下为“ChromaPack / Opaque”的部分代码：
```
half3 YCbCrtoRGB(half y, half cb, half cr)
{
    return half3(
        y                 + 1.402    * cr,
        y - 0.344136 * cb - 0.714136 * cr,
        y + 1.772    * cb
    );
}

half4 frag(v2f_img i) : SV_Target 
{
    float2 uv = i.uv;

    half y  = tex2D(_MainTex, uv * float2(2.0 / 3, 1.0)).a;
    half cb = tex2D(_MainTex, uv * float2(1.0 / 3, 0.5) + float2(2.0 / 3, 0.5)).a - 0.5;
    half cr = tex2D(_MainTex, uv * float2(1.0 / 3, 0.5) + float2(2.0 / 3, 0.0)).a - 0.5;

    return half4(YCbCrtoRGB(y, cb, cr), 1);
}
```
## ⚠️注意事项
由以上说明可知，ChromaPack具有以下特点：
* 内存占用介于ETC/ETC2和Dither之间，bpp = 12；
* 对于不透明图的显示效果优于ETC/ETC2、Dither，对于半透明图在边缘部分，表现较差；
* 对原始纹理尺寸无要求，但纹理尺寸会被扩大，实际以Alpha8格式进行存储；
* 在某些Mali GPU机型上，不支持Alpha8格式，会被软解成RGBA32格式；
* 显示时需要使用特定Shader，且需要进行3次纹理采样；

综上，可以认为ChromaPack适用于无法使用ETC/ETC2格式的较大尺寸纹理。在实际使用时可以根据项目需求进行选择、扩展。




