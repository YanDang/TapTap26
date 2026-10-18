# 音效来源 / SFX provenance

BGM 六段素材的逐文件来源与许可证见 [Music/CREDITS.md](Music/CREDITS.md)。

本表只涵盖本次新增音效。现有 sfx_* 不改动，不对其来源作推断。
全部由本仓库 Tools/audio/synthesize.py 从数学振荡器、滤波白噪声及共振峰原始合成；没有第三方采样、声音字体、录音或真人声音克隆。
来源 URL：无（本地程序生成）。本次生成 WAV 按 CC0-1.0 提供；许可证 https://creativecommons.org/publicdomain/zero/1.0/ 。
目标 LUFS 与实测见 Tools/audio/manifest.json / validation.json；峰值限幅优先于响度目标。短音响度使用短块测量，不能与长节目直接等同。

| 文件 | 来源/许可证 | 合成方法 |
|---|---|---|
| boss_laugh.wav | 本地程序生成 / CC0-1.0 | ha formants 570/950/2250 Hz; breath; F0 93-128Hz; short he + 0.17s ha pairs; 48Hz bed; soft distortion; 1.5s room tail |
| boss_rift_open.wav | 本地程序生成 / CC0-1.0 | rip; dark low-pass / short room |
| boss_rift_close.wav | 本地程序生成 / CC0-1.0 | impact; dark low-pass / short room |
| boss_eye_lock.wav | 本地程序生成 / CC0-1.0 | metal; dark low-pass / short room |
| boss_cleave_whoosh.wav | 本地程序生成 / CC0-1.0 | whoosh; dark low-pass / short room |
| boss_cleave_impact.wav | 本地程序生成 / CC0-1.0 | impact; dark low-pass / short room |
| boss_slam.wav | 本地程序生成 / CC0-1.0 | impact; dark low-pass / short room |
| boss_cloak_sweep.wav | 本地程序生成 / CC0-1.0 | whoosh; dark low-pass / short room |
| boss_beam_charge.wav | 本地程序生成 / CC0-1.0 | charge; dark low-pass / short room |
| boss_beam.wav | 本地程序生成 / CC0-1.0 | beam; dark low-pass / short room |
| boss_transform_rip.wav | 本地程序生成 / CC0-1.0 | rip; dark low-pass / short room |
| boss_hurt.wav | 本地程序生成 / CC0-1.0 | death; dark low-pass / short room |
| boss_posture_break.wav | 本地程序生成 / CC0-1.0 | metal; dark low-pass / short room |
| boss_execution.wav | 本地程序生成 / CC0-1.0 | impact; dark low-pass / short room |
| boss_death.wav | 本地程序生成 / CC0-1.0 | death; dark low-pass / short room |
| boss_claw_swing_1.wav | 本地程序生成 / CC0-1.0 | whoosh; dark low-pass / short room |
| boss_claw_hit_1.wav | 本地程序生成 / CC0-1.0 | impact; dark low-pass / short room |
| boss_claw_swing_2.wav | 本地程序生成 / CC0-1.0 | whoosh; dark low-pass / short room |
| boss_claw_hit_2.wav | 本地程序生成 / CC0-1.0 | impact; dark low-pass / short room |
| boss_claw_swing_3.wav | 本地程序生成 / CC0-1.0 | whoosh; dark low-pass / short room |
| boss_claw_hit_3.wav | 本地程序生成 / CC0-1.0 | impact; dark low-pass / short room |
| boss_eye_hum.wav | 本地程序生成 / CC0-1.0 | periodic 55/82.5/110 Hz eye drone |
| gear_whoosh.wav | 本地程序生成 / CC0-1.0 | whoosh; mechanical transient |
| gear_clang.wav | 本地程序生成 / CC0-1.0 | metal; mechanical transient |
| gear_zap.wav | 本地程序生成 / CC0-1.0 | electric; mechanical transient |
| gear_shell_block.wav | 本地程序生成 / CC0-1.0 | metal; mechanical transient |
| gear_static.wav | 本地程序生成 / CC0-1.0 | electric; mechanical transient |
| gear_water.wav | 本地程序生成 / CC0-1.0 | water; mechanical transient |
| gear_grapple_out.wav | 本地程序生成 / CC0-1.0 | metal; mechanical transient |
| gear_grapple_back.wav | 本地程序生成 / CC0-1.0 | whoosh; mechanical transient |
| gear_decoy_fuse.wav | 本地程序生成 / CC0-1.0 | electric; mechanical transient |
| gear_explosion.wav | 本地程序生成 / CC0-1.0 | impact; mechanical transient |
| gear_jet.wav | 本地程序生成 / CC0-1.0 | whoosh; mechanical transient |
| gear_root.wav | 本地程序生成 / CC0-1.0 | rip; mechanical transient |
| gear_heal.wav | 本地程序生成 / CC0-1.0 | heal; mechanical transient |
| boss_sigh.wav | 本地程序生成（Claude，Tools/audio/make_sigh_steps.py）/ CC0-1.0 | 替换 boss_laugh：吸气 + 长呼气，F0 108→68 Hz，a→o 共振峰下移 0.82，46 Hz 胸腔嗡鸣，1.8 s 混响 |
| boss_step_1–3.wav | 本地程序生成（Claude，同上）/ CC0-1.0 | 62→38 Hz 闷响 + 砂砾噪声 + 刮擦，短混响 |
