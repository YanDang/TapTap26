using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BiomechanicalCrafting.Editor
{
    /// <summary>
    /// Unity 原生精灵图集切片器 (Sprite Sheet Slicer)
    /// 将生体材料总图 (BiomechanicalMaterialsSheet.png) 在 Unity 内置 Sprite Editor 规范下
    /// 分割为 36 个原生子精灵 (Sub-Sprites)，支持在 Unity 检查器中自由二次微调！
    /// </summary>
    public static class BiomechanicalSpriteSheetSlicer
    {
        public const string SHEET_PATH = "Assets/Resources/BiomechanicalMaterialsSheet.png";
        public const int TEXTURE_HEIGHT = 700;

        [MenuItem("BiomechanicalCrafting/Re-slice Material Sprite Sheet in Unity")]
        public static void SliceSpriteSheet()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SHEET_PATH) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[SpriteSlicer] 无法找到图集资源: {SHEET_PATH}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;

            // 原始坐标定义 (PIL坐标系：左上角为原点 (0,0)，宽度 1024, 高度 700)
            // 根据图像原生生体外轮廓精确包围盒，并预留舒适留白，彻底避免切边现象
            var rawDefinitions = new List<Tuple<string, int, int, int, int>>
            {
                // ================= 1. 【气 / 压力】系材料 (第 1 行) =================
                Tuple.Create("mat_gas_bubble_sac", 16, 10, 136, 138),        // 气虫储气泡囊
                Tuple.Create("mat_gas_valve_pipe", 155, 25, 124, 120),       // 承压排气阀管
                Tuple.Create("mat_gas_float_bladder", 285, 20, 115, 125),    // 微压呼吸浮囊 (含顶部小气泡)
                Tuple.Create("mat_gas_vortex_cartilage", 410, 30, 110, 115), // 气旋喷口软骨
                Tuple.Create("mat_gas_inflatable_duct", 532, 25, 125, 122),  // 充气导管结缔组织
                Tuple.Create("mat_gas_aerosol_liquid", 670, 25, 95, 120),    // 气溶胶凝结液
                Tuple.Create("mat_gas_pressure_antenna", 780, 25, 105, 122), // 泄压感应触角
                Tuple.Create("mat_gas_suction_skin", 895, 25, 118, 122),      // 紫斑弹性吸盘皮

                // ================= 2. 【电 / 脉冲】系材料 (第 2 行) =================
                Tuple.Create("mat_elec_clam_shell", 18, 170, 130, 155),       // 砗磲绝缘厚壳
                Tuple.Create("mat_elec_muscle_bundle", 158, 160, 105, 168),   // 电鳗放电肌束 (保留完整下垂神经触须)
                Tuple.Create("mat_elec_electrolyte_drop", 275, 180, 95, 135), // 活体电解液滴
                Tuple.Create("mat_elec_piezo_chip", 380, 190, 100, 125),      // 仿生压电晶片
                Tuple.Create("mat_elec_pulse_membrane", 488, 195, 102, 120),  // 脉冲收缩薄膜
                Tuple.Create("mat_elec_magnetic_coil", 590, 185, 90, 130),    // 生物磁极线圈
                Tuple.Create("mat_elec_silver_nerve", 680, 165, 95, 80),      // 镀银神经索 (右上)
                Tuple.Create("mat_elec_overload_valve", 680, 250, 95, 90),    // 过载泄电阀瓣 (右下)
                Tuple.Create("mat_elec_cooling_ribs", 780, 160, 110, 80),     // 绝缘散热肋片 (右上)
                Tuple.Create("mat_elec_flange_plug", 785, 248, 105, 95),      // 导电法兰插头 (右下)

                // ================= 3. 【木 / 增生】系材料 (第 3 行) =================
                Tuple.Create("mat_wood_coral_plate", 25, 360, 120, 138),      // 增生珊瑚骨板
                Tuple.Create("mat_wood_hollow_coral", 150, 360, 120, 132),    // 多孔空心珊瑚
                Tuple.Create("mat_wood_root_gear", 272, 365, 124, 122),       // 齿轮生根节
                Tuple.Create("mat_wood_symbiotic_fungi", 400, 365, 125, 130), // 荧光共生菌群
                Tuple.Create("mat_wood_calcified_valve", 528, 365, 122, 130), // 钙化木质活门
                Tuple.Create("mat_wood_rubber_seal", 658, 370, 115, 122),     // 弹性橡胶皮垫
                Tuple.Create("mat_wood_titanium_patch", 780, 365, 112, 128),  // 嵌合钛合金贴片
                Tuple.Create("mat_wood_spring_fascia", 898, 362, 115, 130),   // 弹簧藤蔓筋膜

                // ================= 4. 【水 / 潮润】系材料 (第 4 行) =================
                Tuple.Create("mat_water_ventricle", 18, 540, 118, 130),       // 脉动造水心室
                Tuple.Create("mat_water_sponge_filter", 136, 542, 96, 128),   // 纤维海绵滤芯
                Tuple.Create("mat_water_siphon_tube", 232, 545, 104, 122),    // 循环虹吸肉管
                Tuple.Create("mat_water_viscous_gel", 336, 548, 98, 118),     // 高粘度保水凝胶
                Tuple.Create("mat_water_algae_filament", 432, 545, 95, 125),  // 韧性储水藻丝
                Tuple.Create("mat_water_keratin_nozzle", 530, 548, 92, 122),  // 喷淋角质喷头
                Tuple.Create("mat_water_patrol_lens", 622, 552, 100, 112),    // 锈蚀巡逻复眼透镜
                Tuple.Create("mat_water_neon_powder", 725, 550, 95, 118),     // 废弃霓虹荧光粉
                Tuple.Create("mat_water_logic_chip", 826, 550, 85, 115),      // 破损逻辑指令片
                Tuple.Create("mat_water_alloy_grapple", 918, 545, 102, 125)   // 螺纹合金抓钩
            };

            var spriteRectArray = new UnityEditor.SpriteRect[rawDefinitions.Count];
            for (int i = 0; i < rawDefinitions.Count; i++)
            {
                var def = rawDefinitions[i];
                string name = def.Item1;
                int pilX = def.Item2;
                int pilY = def.Item3;
                int pilW = def.Item4;
                int pilH = def.Item5;

                // 转换到 Unity 坐标系 (左下角为 (0,0))
                float unityX = pilX;
                float unityY = TEXTURE_HEIGHT - (pilY + pilH);

                spriteRectArray[i] = new UnityEditor.SpriteRect
                {
                    name = name,
                    rect = new Rect(unityX, unityY, pilW, pilH),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate()
                };
            }

            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            dataProvider.SetSpriteRects(spriteRectArray);
            dataProvider.Apply();

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log($"<color=#4AFF70>[SpriteSheet Slicer]</color> 成功使用 Unity 原生 ISpriteEditorDataProvider 规则分割 {spriteRectArray.Length} 个材料切片！可在 Project 展开查看或在 Sprite Editor 中自由微调。");
        }
    }
}
