# Mode Procedural Content Generation — Tactical Outpost

Mode baru untuk **Modul Praktikum 09 (Seeded, Weighted & Validated Procedural Content Generation)**. Mode ini terpisah dari game Mini Project:

- Setelah **START OPERATION**, pemain memilih **MINI PROJECT** atau **PROCEDURAL CONTENT GENERATION**.
- **Mini Project** tetap alur lama tanpa perubahan. Tidak ada script lama yang diedit; `WaveDirector.cs` dan `EnemyDebugSpawner.cs` sudah dikembalikan ke versi aslinya.
- **Procedural Content Generation** menjalankan arena baru yang lebih sederhana. Arena disusun setiap match dari **prefab yang sudah dipakai di scene game** (tidak ada mesh atau obstacle buatan sendiri).

## Cara kerja tanpa mengubah game lama

`PcgMode` memasang hook `SceneManager.sceneLoaded`:

- **Scene menu:** komponen `PcgModeSelect` ditempel ke GameMenu, lalu tombol START diarahkan ke pemilih mode.
- **Scene game, bila mode PCG dipilih:**
  - `IndustrialGameSession` dimatikan sebelum `Start()` dan diganti `PcgSession`.
  - `PcgSession` memuat environment industrial **hanya untuk langit malam, cahaya bulan, dan post-processing**; semua geometrinya dimatikan.
  - Arena dibangun, NavMesh di-bake ulang, lalu objek gameplay yang sudah ada dipindah ke arena: generator objective, 6 attack point, 21 spawn point zombie, player, play area, dan minimap.
  - Setelah itu sistem gameplay asli dinyalakan apa adanya (WaveDirector, AI zombie, HUD, boss).

## Bagian yang procedural (fokus PCG)

| Content | Teknik |
|---|---|
| **Posisi 3 gerbang** (West, North, South) | Seeded random di sepanjang pagar, dengan jarak minimum dari sudut |
| **Pagar perimeter**, menara floodlight, dan lampu | Disusun dari segmen pagar existing; gerbang dikosongkan |
| **Obstacle/cover** (34): container, concrete barrier, road block, sandbag, dumpster, barel, burning barrel | Weighted random + generate-and-test (6 aturan) + rotasi random |
| **Validasi level** | NavMesh di-bake ulang. Setiap gerbang wajib punya jalur lengkap ke generator untuk zombie **dan** boss, dengan detour ≤ 1,8×. Bila gagal, layout dibuat ulang (stream berikutnya), lalu repair. |
| **Spawn point zombie** (7 per gerbang) | Generate-and-test di zona dalam gerbang (NavMesh, jarak minimum dari player, obstacle, spacing, jalur) |
| **Komposisi zombie tiap wave** | Weighted random dari tabel enemy + difficulty budget per wave (lihat bawah) |
| **Safe zone player** | Radius bebas obstacle/props di sekitar posisi awal player, ditandai garis melingkar hijau yang menyala |
| **Supply cache** (3 per wave) | Weighted table Ammo 60 : Medkit 40, generate-and-test di sekitar generator |
| **Props kecil** (35): cone, cinder block, pallet, barel | Random posisi, rotasi, dan skala. Props padat (collider aktif) dan ikut di-bake ke NavMesh; jalur divalidasi ulang dan props yang memutus jalur dibuang |

Seed yang sama menghasilkan arena yang sama persis (diuji 3× dengan layout hash identik). Semua stream memakai `PcgRandom` (`System.Random` per subsistem), bukan `UnityEngine.Random`.

## Aturan generate-and-test obstacle

1. Masih di dalam pagar (`wallMargin`)
2. Area generator tetap lapang (`generatorClearRadius` 9 m)
3. Mulut gerbang tetap terbuka (`gateClearRadius` 9 m)
4. Player safe zone (`playerClearRadius` 4 m)
5. Minimum spacing antar-obstacle (`obstacleGap` 2,5 m)
6. Tidak menabrak pagar, lampu, menara, atau obstacle lain (`Physics.OverlapBox` + LayerMask)

Ditambah `maxAttempts` (40 per obstacle) dan validasi level (NavMesh) setelah semua obstacle dipasang.

## Enemy selection (weighted random + difficulty budget)

Tabel di `PCG_Profile` (`enemyTable`, `waveBudget`):

| Enemy | Weight | Cost | Mulai wave |
|---|---:|---:|---:|
| walker | 50 | 1 | 1 |
| runner | 25 | 2 | 1 |
| skinless | 15 | 2 | 1 |
| bloater | 10 | 3 | 2 |

Budget per wave: 38 / 58 / 78.

Cara kerjanya:
- Tiap wave, generator memilih zombie secara weighted random, berulang sampai budget habis. Varian yang cost-nya melebihi sisa budget tidak boleh dipilih.
- Jumlah zombie dibuat genap karena WaveDirector men-spawn `perSide × 2`.
- Stream yang dipakai `PcgRandom "enemy"`, jadi seed sama menghasilkan komposisi sama.

`WaveDirector.cs` tidak diubah. Mode PCG hanya mengganti field publik `catalog` dan `waves` pada instance runtime:
- `waves[i].perSide` diisi jumlah zombie hasil rencana / 2.
- `catalog` diganti katalog runtime berisi boss asli dan satu slot zombie biasa. Isi slot ini diganti sesuai antrean rencana setiap kali satu zombie muncul.
- Boss tetap mengikuti WaveDirector (wave 3).

Console mencetak rencana tiap wave dan prefab yang benar-benar muncul, contoh:
`Wave 1 spawned: rencana walker 14 runner 8 skinless 4 | prefab yang muncul: Zombie_Walker 14, Zombie_Runner 8, Zombie_Skinless 4`.

## Player safe zone

- **Obstacle dan props:** tidak boleh berada dalam `playerClearRadius` (6 m) dari posisi awal player.
- **Spawn point zombie:** minimal `minEnemySpawnDistance` (18 m) dari player. Ini rule `minDistanceFromPlayer` di modul; ditolak sebagai `NearPlayer`.
- **Posisi awal player:** selalu di sisi timur generator, sisi yang tidak pernah punya gerbang.
- **Penanda:** tepi safe zone ditandai garis melingkar hijau yang menyala dan tersambung penuh, menempel di lantai. Garisnya dibuat dengan LineRenderer (garis inti + pendar lembut) dan tidak punya collider; warna, kecerahan (`safeZoneGlow`), dan tebal garis (`safeZoneLineWidth`) diatur di `PCG_Profile`.

## Gizmos (Scene View)

`PcgGizmos` ditempel ke objek `PCG_Arena`:

| Gizmo | Arti |
|---|---|
| Putih | Batas area kandidat obstacle |
| Merah | Spawn zone zombie per gerbang |
| Hijau | Safe zone player |
| Kuning | Jarak minimum spawn zombie dari player |
| Oranye | Area lapang generator |
| Cyan | Cincin supply cache |

Pilih `PCG_Arena` di Hierarchy (`OnDrawGizmosSelected`) untuk menampilkan semua kandidat: hijau diterima, merah ditolak.

Menu terkait:
- **Tactical Outpost → PCG → Show Gizmos in Scene View** menyalakan tombol Gizmos Scene View.
- **Toggle Gizmos in Game View** menampilkan gizmo di Game View, misalnya saat merekam.

## Collision

Semua obstacle dan props padat untuk player dan zombie:
- Collider bawaan prefab dipakai. Bila sebuah prefab tidak punya collider sama sekali, setiap mesh-nya diberi BoxCollider seukuran mesh.
- Semua collider obstacle dan props ikut di-bake ke NavMesh, sehingga zombie memutar dan tidak menembus.
- Console mencetak `[PCG] Solid check: obstacle 34/34, props 35/35 punya collider`.

## Panel dan tombol (mode PCG)

Panel PCG ada di kanan atas. Isinya seed, history, posisi gerbang, jumlah obstacle per prefab, penolakan per aturan, laporan jalur tiap gerbang, statistik spawn, supply, dan dressing. Panel detail dan titik kandidat di dunia (hijau = diterima, merah = ditolak) disembunyikan secara default. Tekan F10 untuk menampilkannya.

| Tombol | Fungsi |
|---|---|
| F10 | Tampilkan/sembunyikan panel detail |
| F5 | Reroll supply cache |
| F6 | Arena baru (seed baru) |
| F7 | Ulangi seed yang sama (uji reproducibility) |
| F9 | Eksperimen otomatis → `Docs/PCG/ExperimentResults.md` (termasuk Eksperimen 6: enemy weighted + budget) |
| F8 | Debug AI bawaan game (tetap berfungsi) |

## Parameter

Menu **Tactical Outpost → PCG → Select Profile** (`Assets/TacticalOutpost/Gameplay/Resources/PCG_Profile.asset`).

- Untuk demo reproducibility: matikan `useRandomSeed` dan isi `seed`.
- **Create or Update Profile** mengisi ulang tabel prefab. Prefab yang tidak dipakai di scene Solo, Game, atau Menu otomatis dilewati.

## File

- `Assets/TacticalOutpost/Gameplay/Scripts/PCG/`
  - `PcgMode`: hook mode
  - `PcgModeSelect`: pemilih mode
  - `PcgSession`: orkestrasi, supply, dan eksperimen
  - `PcgArena` + `PcgArenaBuilder`: generator arena dan validasi
  - `PcgDebugView`: panel
  - `PcgWavePlanner`: komposisi zombie (weighted + budget)
  - `PcgGizmos`: Gizmos Scene View
  - `PcgProfile`: parameter
  - `PcgRandom`: seeded RNG
- `Assets/TacticalOutpost/Editor/PcgProfileSetup.cs`
- Versi PCG sebelumnya (yang sempat mengubah WaveDirector) diarsipkan di `Docs/PCG/_old_v1/`, tidak dikompilasi.

## Batasan

- Pencahayaan mengambil objek yang sudah di-setup di map industrial. Saat mode PCG mulai, objek berikut disalin sebagai template:
  - generator beserta 14 floodlight merah dan dust beam-nya,
  - menara jaga dengan floodlight,
  - lampu jalan "Roadside lamp".
  Jangkauan floodlight merah dipersempit dari 45 m ke 16 m dan intensitasnya diturunkan ke 30%, supaya merahnya hanya mengelilingi generator di arena yang lebih kecil.
- Collider mesh yang tidak Read/Write di-bake ke NavMesh sebagai kotak (`mesh.bounds`). Akibatnya bentuk lekuk obstacle sedikit disederhanakan.
- Cinematic ledakan generator tidak dipakai di mode PCG (targetnya bangunan industrial); kalah memakai layar akhir biasa.
- Belum diuji di standalone build.
