# Mode Final Project — Tactical Outpost: BLACKOUT (MODE 04)

Kartu keempat di layar **CHOOSE YOUR OPERATION**: **MODE 04 — FINAL OPERATION**. Map tetap map industrial statis Mini Project (tidak ada objek yang di-spawn acak). Di atas map dan wave asli ditambahkan tiga sistem yang saling terhubung:

```text
Player Performance ──► Dynamic Difficulty Adjustment ──► PCG (event wave) + parameter wave/zombie + bobot Utility AI sekutu ──► Perubahan gameplay
 (akurasi, damage diterima,   (skor performa → skill EMA →     (jumlah zombie, interval spawn, HP/speed zombie,
  HP generator hilang, kill)    difficulty, maxStep 0,2)         bobot varian, drop chance, event, perilaku sekutu)
```

Mini Project, PCG, dan Procedural Dungeon tetap jalan seperti sebelumnya. File lama yang disentuh: `EnemyBrain.cs` (hook sekutu, kosong di mode lain) dan `PcgModeSelect.cs` (kartu ke-4). Backup asli: `Docs/Final/Backup/*.orig`.

## Cara mencoba

1. Play → **START OPERATION** → **MODE 04 FINAL OPERATION**.
2. Panel kanan atas: seed, DDA, event aktif/berikutnya, status sekutu. **F10** = detail.
3. Tag biru di atas sekutu: callsign + aksi Utility AI + HP.

| Tombol | Fungsi |
|---|---|
| F10 | Panel detail: indikator performa live, skor DDA, output DDA, skor utility sekutu, titik taktis di dunia, log |
| F5 | DDA on/off (off = nilai asli Mini Project, untuk pembanding) |
| F6 / F7 | Paksa difficulty +0,2 / −0,2 (demo) |
| F9 | Eksperimen otomatis → `Docs/Final/ExperimentResults.md` |
| F8 → F1..F4 | Debug bawaan game (spawn / kill-all) |
| ENTER (setelah kalah) | Ulangi seed yang sama, difficulty diturunkan `retryRelief` (0,15) |

Parameter: **Tactical Outpost → Final → Create or Select Profile** (`Resources/Final/Final_Profile.asset`).

## 1. DDA (Dynamic Difficulty Adjustment)

**Indikator** (`PerformanceTracker`, dari event yang sudah ada, tanpa mengubah script player):

| Indikator | Sumber | Normalisasi (0 = buruk, 1 = bagus) | Bobot |
|---|---|---|---:|
| Akurasi | `FpsWeapon.OnFired / OnHit` | 15% → 0, 60% → 1 (< 10 tembakan = 0,5) | 0,30 |
| Damage diterima / menit | `PlayerHealth.OnHealthChanged` | 1 − dmg/60 | 0,25 |
| HP generator hilang / menit | `GeneratorHealth.OnDamaged` | 1 − loss/150 | 0,25 |
| Kill / menit (player saja) | `EnemyHealth.AnyDied` | kill/20 | 0,20 |

**Model** (`DdaModel`): `performance` → `skill = lerp(skill, performance, 0,6)` → `difficulty += clamp(skill − difficulty, ±0,2)`. Evaluasi tiap awal wave (performa wave sebelumnya). Pratinjau bergulir tiap 0,5 detik mengisi definisi wave berikutnya sebelum banner muncul.

**Output** (easy → hard):

| Parameter | Easy (0) | Hard (1) | Diterapkan ke |
|---|---:|---:|---|
| Jumlah zombie | ×0,75 | ×1,30 | `WaveDirector.waves[i].perSide` |
| Interval spawn | 0,60 s | 0,30 s | `WaveDirector.spawnInterval` |
| HP zombie | ×0,85 | ×1,20 | config clone per zombie |
| Speed zombie | ×0,90 | ×1,10 | config clone per zombie |
| Drop chance | 40% | 18% | `DropManager.normalDropChance` |
| Bobot varian | walker dominan | runner/skinless/bloater naik | `EnemyCatalog` runtime |
| Bobot Utility AI sekutu | support/repair naik | intercept naik | `AllyBrain` |
| Budget event PCG | kecil | besar | `WaveEventPlanner` |

## 2. PCG (tanpa spawn objek map)

**a. Event wave** (`WaveEventPlanner`): weighted random (bobot bergeser oleh difficulty) + budget + generate-and-test, seeded `System.Random` per wave.

| Event | Cost | Mulai wave | Efek |
|---|---:|---:|---|
| Clear Night | 0 | 1 | — |
| Supply Drop | −0,5 | 1 | 4 ammo/medkit di sekitar generator |
| Dense Fog | 1 | 1 | Fog exponential |
| Blackout | 1,5 | 2 | Lampu 25%, ambient 40%, sight zombie ×0,7 |
| Frenzy | 2 | 2 | Run speed zombie ×1,2, cooldown ×0,8 |
| Horde Rush | 2,5 | 2 | Zombie ×1,3, interval ×0,6 |

Aturan: **MinWave**, **Budget** (`0,5 + 0,75·(wave−1) + 2·difficulty`), **NoRepeat**, **NoHeavyStreak** (cost ≥ 2 tidak berturut-turut), **FinalNoHorde**. Difficulty < 0,35 → Supply Drop tambahan (assist).

**b. Titik taktis sekutu** (`TacticalPoints`): 160 kandidat seeded di cincin 5–18 m sekitar generator → tes NavMesh, jarak dari attack point zombie, jalur ke generator → skor (pandangan ke lane, cover, pandangan ke generator, jarak) → top-8 dengan spacing 4 m. Hanya posisi, tidak membuat objek di map.

## 3. Game AI — NPC sekutu "VIPER" (Utility AI)

Model Insurgent dan AK-47 diambil dari player (clone), pose dihitung dengan IK dua-tulang seperti `FpsBody`.

| Aksi | Skor |
|---|---|
| DEFEND | 0,35 (+0,15 bila tidak ada ancaman) → titik taktis terbaik |
| INTERCEPT | w_intercept × (zombie dalam 9 m dari generator / 4) → titik taktis yang melihat generator & dekat ancaman |
| SUPPORT | w_support × ((1−HP player)^1,5 × 0,9 + zombie dekat player/4 × 0,6) → di samping player |
| REPAIR | w_repair × (1−HP generator) × 1,6 × (aman ? 1 : 0,15) → `GeneratorHealth.Repair()` |
| RETREAT | HP sendiri < 45%: (1 − HP/0,45) × 1,2 → titik paling jauh dari zombie |

Dievaluasi tiap 0,3 detik, hysteresis +0,1. Lapisan tembak paralel: target = zombie dengan skor ancaman tertinggi yang terlihat (dekat generator, dekat player, boss, bela diri). Tembakan sekutu membuat zombie menoleh (`EnemyBrain.AllyNoise`, `DamageSource`), zombie bisa menyerang sekutu. HP 0 → DOWN 15 detik → kembali di generator.

## Verifikasi

- Compile Unity 6000.3.23f1: 0 error.
- Smoke test Play Mode otomatis (`Tactical Outpost → Final → Smoke Test`): mode Final berjalan 3 wave sampai selesai, difficulty 0,50 → 0,70 → 0,79, zombie 24 → 40 → 56, event terpilih tiap wave, sekutu menembak dan pindah aksi; Mini Project tetap 24 zombie wave 1, `Allies = 0`.
- Belum: uji main manual penuh dengan input player, standalone build.

## File

- `Assets/TacticalOutpost/Gameplay/Scripts/Final/`: `FinalMode`, `FinalProfile`, `FinalSession`, `PerformanceTracker`, `DdaModel`, `WaveEventPlanner`, `TacticalPoints`, `AllyBrain`, `AllyBody`, `IAllyTarget`, `FinalDebugView`, `FinalExperiments`
- `Assets/TacticalOutpost/Editor/`: `FinalSetup`, `FinalSmokeTest`
- `Docs/Final/`: README, ExperimentResults.md, SessionLog.csv (1 baris per evaluasi, untuk analisis EAS)
