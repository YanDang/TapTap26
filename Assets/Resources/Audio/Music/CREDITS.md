# 原创合成音乐 / Music credits

本目录六份音频均为本任务中新编写的程序原创合成，不使用现成录音、商业曲目、采样包、SoundFont、参考曲旋律或第三方音乐素材。

- 制作：Hermes Agent / gpt-6-astra，受项目所有者委托。
- 完整可复现源：`Tools/audio/music/synthesize.py`（固定随机种子 181026）。
- 方法：正弦谐波加法合成、失谐弦乐音色、管风琴分音、合成低鼓、确定性噪声、环形立体声延迟混响。
- 素材来源 URL：不适用（本地原创程序生成，无外部素材下载）。
- 授权：本次生成的音频在可适用权利范围内以 **CC0 1.0 Universal** 提供，可商用、修改、分发，无署名要求。无第三方音频权利声明。
- 许可证文本：https://creativecommons.org/publicdomain/zero/1.0/legalcode
- NumPy / SciPy 只是计算工具，未复制其示例音乐或任何音源。

| 文件 | 内容 | 时长 / 节拍 |
|---|---|---|
| `workshop.wav` | 海底工坊：柔和弦垫、稀疏钟音，D 小调 add9 | 80s；72 BPM；24 小节；循环 |
| `boss_phase1.wav` | 夜王：低弦/管风琴、低鼓、八音动机 | 64s；90 BPM；24 小节；循环 |
| `boss_phase2.wav` | 同主题同和声网格，加弦层、180 BPM double-time 脉冲 | 64s；90 BPM 基础拍；循环 |
| `ult_rise.wav` | 不祥阶梯上行 + 低频连续上行，独立 stinger | 3.6s；不循环 |
| `ult_impact.wav` | 劈下低鼓与低音和弦重音（不是 B 的爆炸 SFX） | 1.8s；不循环 |
| `victory.wav` | D 小调向明亮高音域上行的短乐句 | 6s；不循环 |

全部为 24 kHz / 16-bit / stereo PCM WAV。循环使用环形混响尾和 20ms 平滑接缝处理，保留完整小节长度；Unity meta 固定 PCM / preload，避免有损转码改变接缝。B 任务的每条音效 300KB 限制不套用到 C 的分钟级 BGM。
验证：`python Tools/audio/music/validate.py`；数据与波形见 `Tools/audio/music/metrics.json`、`waveforms.png`。RMS 不是 LUFS；感知响度/true peak 独立见 `loudness.json`。
