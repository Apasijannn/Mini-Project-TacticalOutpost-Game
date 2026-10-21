# Tactical Outpost — Gameplay Presentation Brief

Dokumen ini merangkum implementasi gameplay Tactical Outpost yang ada di project saat ini. Angka wave dan boss mengacu pada konfigurasi scene gameplay; angka atribut enemy dan senjata mengacu pada config/prefab yang dipakai runtime.

## Elevator pitch

**Tactical Outpost** adalah game FPS survival single-player. Player mempertahankan satu generator dari serangan zombie yang datang melalui tiga jalur. Ada tiga wave, dengan boss muncul di wave terakhir. Zombie bisa mengejar player atau menyerang generator berdasarkan persepsi dan utility score.

## Kondisi menang dan kalah

- **Menang:** seluruh musuh di wave terakhir mati.
- **Kalah:** HP player habis atau HP generator mencapai nol.
- Setelah mati atau generator hancur, player bisa menekan **Enter** untuk memulai ulang gameplay.
- Generator adalah objective tunggal dengan **400 HP**. Tembakan player tidak merusaknya.
- Script generator memiliki method `Repair()`, tetapi saat ini tidak ada gameplay call yang mengaktifkan repair. Jadi belum ada mekanik repair generator untuk player.

## HP dan damage melee

Damage di tabel adalah damage **per hit yang berhasil**. Cooldown menunjukkan jeda antarserangan; angka damage bukan DPS.

| Entity | HP | Damage ke player / hit | Damage ke generator / hit | Cooldown | Jarak serang | Jalan / lari |
|---|---:|---:|---:|---:|---:|---:|
| Player | 100 | — | — | — | — | 4,2 / 6,6 m/s saat sprint |
| Generator | 400 | — | — | — | — | — |
| Walker | 110 | 10 | 15 | 1,5 dtk | 1,7 m | 1,1 / 3,0 m/s |
| Bloater | 150 | 14 | 21 | 1,7 dtk | 1,7 m | 0,9 / 2,4 m/s |
| Runner | 80 | 9 | 13,5 | 1,2 dtk | 1,7 m | 1,4 / 3,9 m/s |
| Skinless | 90 | 13 | 19,5 | 1,1 dtk | 1,7 m | 1,5 / 4,3 m/s |
| Boss `boss_v1` | 1.000 | 35 | 87,5 | 1,5 dtk | 2,5 m | 2,4 / 5,2 m/s |

Zombie biasa memberi **1,5×** damage ke generator; boss memberi **2,5×**. Damage ke player tetap mengikuti nilai dasar di tabel. Serangan zombie biasa memberikan damage sekitar 0,45 detik setelah animasi serangan dimulai; boss sekitar 0,55 detik.

## Senjata player

Tembakan menggunakan raycast hitscan dari kamera player.

| Senjata | Damage badan | Rate of fire | Magazine + reserve awal | Maks. reserve | Reload | Jarak | Pickup ammo |
|---|---:|---:|---:|---:|---:|---:|---:|
| AK-47 | 28 | 600 RPM | 30 + 120 | 240 | 2,1 dtk | 250 m | 60 |
| Pistol | 36 | 420 RPM | 12 + 48 | 96 | 1,5 dtk | 160 m | 24 |

- Headshot mengalikan damage **3×** pada zombie biasa dan **2×** pada boss. Jadi AK memberikan 84/56 damage per headshot dan pistol 108/72.
- Perkiraan peluru untuk membunuh dengan body shot yang semuanya mengenai target: AK **4/6/3/4/36** untuk Walker/Bloater/Runner/Skinless/Boss; pistol **4/5/3/3/28**. Ini hitungan teoritis dari HP dan damage, tanpa miss.
- Player mulai dengan AK sebagai senjata aktif. **1/2**, **Q**, atau scroll mouse mengganti senjata.
- Spread membesar saat bergerak dan melompat; spread mengecil saat crouch atau ADS. Ada recoil, hit marker, crosshair dinamis, muzzle flash, dan impact effect.
- Tembakan menghasilkan noise dalam radius senjata: AK **28 m**, pistol **20 m**. Zombie dapat menyelidiki sumber suara.

## AI zombie dan boss

AI adalah implementasi C# custom: **FSM untuk state perilaku, ditambah utility scoring untuk memilih target**. Tidak ada Behavior Tree yang dipakai.

State di `EnemyBrain`:

`Spawning → Advance / Chase → AttackGenerator / AttackPlayer`, dengan state tambahan `Stagger` dan `Dead`.

Pengambilan keputusan berjalan berkala (sekitar 0,15 detik). Zombie memakai `NavMeshAgent` untuk bergerak.

- Persepsi player memakai jarak pandang, FOV, dan line of sight. Di close-sense radius, player terdeteksi tanpa perlu berada dalam cone pandang.
- Zombie biasa memiliki sight radius **22 m** (Skinless **26 m**), FOV **150°**, close-sense **3 m**, dan ingatan posisi terakhir **8 detik**.
- Boss memiliki sight radius **45 m**, FOV **220°**, close-sense **8 m**, hearing lebih kuat, dan ingatan posisi **14 detik**.
- Tembakan mengirim noise event. Saat terkena tembakan, zombie memperoleh informasi posisi player.
- Player yang terlihat/didengar dibandingkan dengan generator melalui skor. Skor generator dipengaruhi jarak, sisa HP generator, jumlah attacker lain, dan bonus kecil untuk mempertahankan target yang sama. Zombie cenderung membagi serangan ke attack point yang berbeda.
- Generator memiliki enam attack point. Jalur dari spawn ke seluruh attack point dicatat terhubung di data validasi NavMesh.
- Zombie biasa dapat stagger jika satu hit mencapai **12% HP maksimal**; Bloater menggunakan **20%**. Durasi stagger **0,35 detik**. Boss tidak dapat di-stagger.
- Boss mulai enrage di **50% HP**: speed bertambah **30%**, cooldown menjadi **70%** dari nilai awal (1,05 detik), dan boss mengeluarkan roar. Scale runtime boss di scene ditargetkan sekitar **2,3×** zombie biasa.

### Komposisi varian

Pemilihan varian zombie biasa menggunakan weighted random yang sama untuk semua wave:

| Varian | Bobot | Proporsi perkiraan |
|---|---:|---:|
| Walker | 3 | 35,3% |
| Bloater | 1,5 | 17,6% |
| Runner | 2 | 23,5% |
| Skinless | 2 | 23,5% |

Persentase adalah ekspektasi, bukan jatah pasti per wave. Catalog memiliki tiga boss variant, tetapi scene gameplay mengunci `boss_v1`.

## Wave dan tempo permainan

| Wave | Zombie biasa | Boss | Total musuh baru | Normal per lane |
|---|---:|---:|---:|---:|
| 1 | 24 | 0 | 24 | 8 |
| 2 | 36 | 0 | 36 | 12 |
| 3 | 48 | 3 | 51 | 16 + 1 boss |
| **Total** | **108** | **3** | **111** | — |

1. Player mendapat countdown **6 detik** (`READY`).
2. Setiap wave menampilkan banner selama **3 detik**. Normal zombie muncul dengan jeda **0,45 detik**.
3. Timer wave berikutnya adalah **60 detik**, dimulai saat banner wave ditampilkan.
4. Wave berikutnya dijadwalkan saat semua zombie wave berjalan mati **atau** timer habis—mana yang lebih dulu. Zombie yang tersisa tetap aktif dan terbawa saat wave baru dimulai.
5. Spawn yang sudah dijadwalkan untuk wave saat ini diselesaikan dulu. Jadi timer habis tidak membatalkan sisa spawn wave tersebut.
6. Wave 3 menampilkan warning merah **“BOSSES INBOUND”**. Setelah jeda boss **3,5 detik**, satu boss muncul di tiap lane.
7. Wave 3 adalah wave final; kemenangan menunggu sampai semua musuh mati.

Jika player tidak membunuh zombie, total musuh aktif secara teoritis dapat mencapai **111**. Jumlah ini juga menjadi pertimbangan penting untuk performance dan balancing.

## Spawn dan navigasi map

Terdapat **21 titik spawn**: tujuh di tiap lane **West, North, South**. Normal zombie dibagi bergiliran antarlane; urutan titik dalam tiap lane diacak dan posisi spawn diberi jitter kecil (sekitar 0,6 m). Boss Wave 3 memakai satu titik di tiap lane.

| Lane | Sebaran koordinat spawn |
|---|---|
| West | x ≈ −60; z ≈ −12,16 sampai 3,60 |
| North | z ≈ 55; x ≈ −5,44 sampai 10,56 |
| South | z ≈ −55; x ≈ 12,12 sampai 27,12 |

Player muncul di jalan depan generator, di antara dua tower depan: posisi kira-kira **(−4,2; 0,55; −8)**, yaw sekitar **183°**.

Catatan authoring navigation mencatat semua 21 spawn dapat mencapai keenam attack point, dengan **8.529 NavMesh vertices** dan `allConnected: true`. Zombie berjalan dengan NavMesh; tidak ada sistem lompat atau panjat untuk melewati permukaan yang terputus.

## Pickup, HUD, dan menu

- Zombie biasa memiliki peluang drop **30%**. Jenis drop menyesuaikan kondisi: medkit lebih mungkin saat HP rendah dan ammo lebih mungkin saat reserve menipis.
- Boss selalu menjatuhkan **satu ammo pickup dan satu health pickup**.
- Health pickup memulihkan **35 HP**, dapat diambil dalam radius **1,7 m**, dan menghilang setelah **45 detik**. Maksimum **14 pickup** aktif; pickup tertua dibersihkan saat batas terlampaui.
- HUD menampilkan HP player, ammo, crosshair, wave/countdown, jumlah zombie, dan HP generator. Boss diberi warning merah.
- **M** membuka/menutup minimap besar. **Esc** menutup minimap besar dulu; Esc berikutnya membuka pause. Zombie biasa berwarna kuning dan boss merah, dengan edge arrow untuk target di luar area map.
- Main menu menyediakan **Start** dan **Exit**. Pause menu menyediakan **Resume** dan **Exit**; Exit dari pause kembali ke main menu.

## Batasan implementasi saat ini

- Single-player; tidak ada co-op atau friendly AI dalam gameplay ini.
- Tidak ada gameplay repair generator aktif.
- Zombie tidak lompat/memanjat NavMesh yang terputus.
- Komposisi varian zombie biasa tidak meningkat berdasarkan wave; weighted random tetap sama.
- Boss gameplay saat ini selalu `boss_v1`, walaupun catalog menyimpan variant lain.
- Belum ada hasil standalone build atau long-session performance test pada skenario puncak 111 musuh.

## File sumber utama

- [WaveDirector](Assets/TacticalOutpost/Gameplay/Scripts/Enemy/WaveDirector.cs)
- [EnemyBrain](Assets/TacticalOutpost/Gameplay/Scripts/Enemy/EnemyBrain.cs)
- [EnemyConfig](Assets/TacticalOutpost/Gameplay/Scripts/Enemy/EnemyConfig.cs)
- [PlayerHealth](Assets/TacticalOutpost/Gameplay/Scripts/Player/PlayerHealth.cs)
- [FpsWeapon](Assets/TacticalOutpost/Gameplay/Scripts/Player/FpsWeapon.cs)
- [GeneratorHealth](Assets/TacticalOutpost/Gameplay/Scripts/Enemy/GeneratorHealth.cs)
- [Industrial enemy catalog](Assets/TacticalOutpost/Gameplay/Enemies/Adapted/EnemyCatalog_Industrial.asset)
- [Spawn layout](Docs/Integration/SpawnLayout.json)
- [Player spawn](Docs/Integration/PlayerSpawn.json)
- [Navigation authoring](Docs/Integration/NavigationAuthoring.json)
- [Timed wave check record](Docs/Integration/TimedWaveChecks.json)

> Angka konfigurasi di dokumen ini merepresentasikan scene/prefab gameplay yang sekarang. Catatan lama pada dokumen integrasi dapat berisi nilai wave yang sudah tidak dipakai.
