using UnityEngine;
using UnityEditor;
using System.Collections;

class ChromaPackProcessor : AssetPostprocessor
{
    static float RGB_Y(Color rgb)
    {
        return 0.299f * rgb.r + 0.587f * rgb.g + 0.114f * rgb.b;
    }

    static float RGB_Ya(Color rgb)
    {
        if (rgb.a < 0.5f)
            return 0;
        else
            return RGB_Y(rgb) * 255 / 256 + 1.0f / 256;
    }

    static float RGB_Cb(Color rgb)
    {
        return -0.168736f * rgb.r - 0.331264f * rgb.g + 0.5f * rgb.b;
    }

    static float RGB_Cr(Color rgb)
    {
        return 0.5f * rgb.r - 0.418688f * rgb.g - 0.081312f * rgb.b;
    }

    void OnPreprocessTexture()
    {
        var importer = assetImporter as TextureImporter;

        if (!assetPath.EndsWith("CP.png")) return;

        importer.textureType = TextureImporterType.Default;
        importer.textureFormat = TextureImporterFormat.RGBA32;
    }

    void OnPostprocessTexture(Texture2D texture)
    {
        var importer = assetImporter as TextureImporter;

        var hasAlpha = importer.DoesSourceTextureHaveAlpha();
       
        //ChromaPack处理
        if (assetPath.EndsWith("CP.png"))
        {
            var source = texture.GetPixels();

            var tw = texture.width;
            var th = texture.height;

            texture.Resize(tw * 3 / 2, th, TextureFormat.Alpha8, false);

            var pixels = texture.GetPixels();

            var i1 = 0;
            var i2 = 0;

            if (hasAlpha)
            {
                for (var iy = 0; iy < th; iy++)
                {
                    for (var ix = 0; ix < tw; ix++)
                    {
                        pixels[i2++].a = RGB_Ya(source[i1++]);
                    }
                    i2 += tw / 2;
                }
            }
            else
            {
                for (var iy = 0; iy < th; iy++)
                {
                    for (var ix = 0; ix < tw; ix++)
                    {
                        pixels[i2++].a = RGB_Y(source[i1++]);
                    }
                    i2 += tw / 2;
                }
            }

            i1 = 0;
            i2 = tw;
            var i3 = (tw * 3 / 2) * th / 2 + tw;

            for (var iy = 0; iy < th / 2; iy++)
            {
                for (var ix = 0; ix < tw / 2; ix++)
                {
                    var ws = (source[i1] + source[i1 + 1] + source[i1 + tw] + source[i1 + tw + 1]) / 4;
                    pixels[i2++].a = RGB_Cr(ws) + 0.5f;
                    pixels[i3++].a = RGB_Cb(ws) + 0.5f;
                    i1 += 2;
                }
                i1 += tw;
                i2 += tw;
                i3 += tw;
            }
            texture.SetPixels(pixels);
        }

        //dither565处理
        if(assetPath.Contains("dither") && !hasAlpha)
        {
            var pixels = texture.GetPixels();
            var tw = texture.width;
            var th = texture.height;

            var offs = 0;

            var k1Per31 = 1.0f / 31.0f;

            var k1Per32 = 1.0f / 32.0f;
            var k5Per32 = 5.0f / 32.0f;
            var k11Per32 = 11.0f / 32.0f;
            var k15Per32 = 15.0f / 32.0f;

            var k1Per63 = 1.0f / 63.0f;

            var k3Per64 = 3.0f / 64.0f;
            var k11Per64 = 11.0f / 64.0f;
            var k21Per64 = 21.0f / 64.0f;
            var k29Per64 = 29.0f / 64.0f;

            var k_r = 32; //R&B压缩到5位，所以取2的5次方
            var k_g = 64; //G压缩到6位，所以取2的6次方

            for (var y = 0; y < th; y++)
            {
                for (var x = 0; x < tw; x++)
                {
                    float r = pixels[offs].r;
                    float g = pixels[offs].g;
                    float b = pixels[offs].b;

                    var r2 = Mathf.Clamp01(Mathf.Floor(r * k_r) * k1Per31);
                    var g2 = Mathf.Clamp01(Mathf.Floor(g * k_g) * k1Per63);
                    var b2 = Mathf.Clamp01(Mathf.Floor(b * k_r) * k1Per31);

                    var re = r - r2;
                    var ge = g - g2;
                    var be = b - b2;

                    var n1 = offs + 1;
                    var n2 = offs + tw - 1;
                    var n3 = offs + tw;
                    var n4 = offs + tw + 1;

                    if (x < tw - 1)
                    {
                        pixels[n1].r += re * k15Per32;
                        pixels[n1].g += ge * k29Per64;
                        pixels[n1].b += be * k15Per32;
                    }

                    if (y < th - 1)
                    {
                        pixels[n3].r += re * k11Per32;
                        pixels[n3].g += ge * k21Per64;
                        pixels[n3].b += be * k11Per32;

                        if (x > 0)
                        {
                            pixels[n2].r += re * k5Per32;
                            pixels[n2].g += ge * k11Per64;
                            pixels[n2].b += be * k5Per32;
                        }

                        if (x < tw - 1)
                        {
                            pixels[n4].r += re * k1Per32;
                            pixels[n4].g += ge * k3Per64;
                            pixels[n4].b += be * k1Per32;
                        }
                    }

                    pixels[offs].r = r2;
                    pixels[offs].g = g2;
                    pixels[offs].b = b2;

                    offs++;
                }
            }
            texture.SetPixels(pixels);
            EditorUtility.CompressTexture(texture, TextureFormat.RGB565, TextureCompressionQuality.Best);

        }

        //dither4444处理
        if (assetPath.Contains("dither") && hasAlpha)
        {
            var pixels = texture.GetPixels();
            var tw = texture.width;
            var th = texture.height;

            var offs = 0;

            var k1Per15 = 1.0f / 15.0f;
            var k1Per16 = 1.0f / 16.0f;
            var k3Per16 = 3.0f / 16.0f;
            var k5Per16 = 5.0f / 16.0f;
            var k7Per16 = 7.0f / 16.0f;

            for (var y = 0; y < th; y++)
            {
                for (var x = 0; x < tw; x++)
                {
                    float a = pixels[offs].a;
                    float r = pixels[offs].r;
                    float g = pixels[offs].g;
                    float b = pixels[offs].b;

                    var a2 = Mathf.Clamp01(Mathf.Floor(a * 16) * k1Per15);
                    var r2 = Mathf.Clamp01(Mathf.Floor(r * 16) * k1Per15);
                    var g2 = Mathf.Clamp01(Mathf.Floor(g * 16) * k1Per15);
                    var b2 = Mathf.Clamp01(Mathf.Floor(b * 16) * k1Per15);

                    var ae = a - a2;
                    var re = r - r2;
                    var ge = g - g2;
                    var be = b - b2;

                    pixels[offs].a = a2;
                    pixels[offs].r = r2;
                    pixels[offs].g = g2;
                    pixels[offs].b = b2;

                    var n1 = offs + 1;
                    var n2 = offs + tw - 1;
                    var n3 = offs + tw;
                    var n4 = offs + tw + 1;

                    if (x < tw - 1)
                    {
                        pixels[n1].a += ae * k7Per16;
                        pixels[n1].r += re * k7Per16;
                        pixels[n1].g += ge * k7Per16;
                        pixels[n1].b += be * k7Per16;
                    }

                    if (y < th - 1)
                    {
                        pixels[n3].a += ae * k5Per16;
                        pixels[n3].r += re * k5Per16;
                        pixels[n3].g += ge * k5Per16;
                        pixels[n3].b += be * k5Per16;

                        if (x > 0)
                        {
                            pixels[n2].a += ae * k3Per16;
                            pixels[n2].r += re * k3Per16;
                            pixels[n2].g += ge * k3Per16;
                            pixels[n2].b += be * k3Per16;
                        }

                        if (x < tw - 1)
                        {
                            pixels[n4].a += ae * k1Per16;
                            pixels[n4].r += re * k1Per16;
                            pixels[n4].g += ge * k1Per16;
                            pixels[n4].b += be * k1Per16;
                        }
                    }

                    offs++;
                }
            }
            texture.SetPixels(pixels);
            EditorUtility.CompressTexture(texture, TextureFormat.RGBA4444, TextureCompressionQuality.Best);
        }


       
    }
}
