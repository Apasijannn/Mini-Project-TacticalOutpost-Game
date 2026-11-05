# Mode Procedural Dungeon — Tactical Outpost (Modul Praktikum 10)

Mode ketiga di layar **CHOOSE YOUR OPERATION** (kartu **MODE 03 — PROCEDURAL DUNGEON**). Mini Project dan mode PCG tidak berubah; satu-satunya file lama yang disentuh adalah `PcgModeSelect.cs` (2 kartu → 3 kartu, backup di `Docs/Dungeon/Backup/PcgModeSelect.cs.orig`).

**Objective:** player mulai di Start room (safe zone = kotak hijau menyala mengikuti sisi-sisi Start room) dan harus menyalakan **generator di Goal room** → `POWER RESTORED`. Sebelum itu ada tiga objektif:

| # | Objektif | Cara kerja |
|---|---|---|
| 1 | **Power relay (lock-and-key)** | Generator terkunci sampai 3 relay (`Electric_box_v3`, lampu merah → hijau) diaktifkan dengan **E**. Relay ditempatkan procedural: bukan Start/Goal/safe room, saling berjauhan (≥ 10 cell, farthest-point sampling), ≥ 1 relay di room yang tidak dilewati critical path, 1 zombie penjaga per relay. Hard constraint: BFS dengan Goal tertutup harus mencapai semua relay. |
| 2 | **Clear room** | Zombie room baru di-spawn saat player masuk room → semua bukaan room ditutup segmen dinding yang naik dari lantai (+ NavMeshObstacle carve), zombie room itu langsung sadar posisi player. Pintu turun setelah zombie room habis (`ROOM CLEARED`). Relay di room tersegel baru bisa diaktifkan setelah room bersih. Pengaman: timeout 120 s. |
| 3 | **Boss** | Setelah relay terakhir aktif, boss muncul saat player masuk Goal room (`BOSS AWAKE`). Generator baru bisa dinyalakan setelah boss mati. |

Path guide di lantai menunjuk relay belum aktif terdekat (jarak BFS), lalu generator. Ammo/medkit tersebar.

**Spawn per room:** posisi zombie ditentukan generator saat dungeon dibuat, tetapi zombie baru muncul ketika player masuk ke dalam room tersebut (melewati mulut pintu). Sebelumnya room kosong, jadi zombie tidak bisa keluar mengejar player dan player tidak bisa menembak zombie dari luar. Zombie yang posisinya < 4 cell dari player dipindah ke sisi room yang lebih jauh. Combat room langsung tersegel (clear room); Goal room: zombie muncul saat masuk, boss muncul saat player masuk Goal room setelah semua relay aktif. Random Walk (tanpa room): zombie muncul saat player dalam radius 7 cell. Atur di `spawnMinPlayerCells` / `looseSpawnCells`.

## Pipeline

```text
Seed → Grid TileType[60,40] → BSP split (rasio 1.25) → Room di Leaf (roomMargin) → L-corridor antar-sibling BSP (+ extra loop)
     → Start (room pertama) & Goal (strategi A/B/C) → Validasi BFS (hard constraint) → Repair (carve corridor) / generate ulang (max 10)
     → Props (di luar critical path) → Enemy & Boss → Item (dead-end / side room) → Validasi BFS akhir (props = penghalang) → repair props
     → Build scene (prefab existing) → NavMesh bake → validasi NavMesh start → goal → spawn → play
```

| File | Isi |
|---|---|
| `Scripts/Dungeon/DungeonData.cs` | `TileType`, `DungeonRoom`, `BSPNode`, `DungeonLayout` (grid, room, placement, metrics, hash) |
| `Scripts/Dungeon/DungeonGenerator.cs` | BSP, Random Walk, corridor, start/goal, placement + constraint, BFS validation, repair, generate-and-test, metrics, ASCII map |
| `Scripts/Dungeon/DungeonBuilder.cs` | Grid → GameObject (floor per cell, dinding hanya di sisi yang menghadap floor, kolom sudut, lampu, goal, props) + NavMesh |
| `Scripts/Dungeon/DungeonObjectives.cs` | Relay (E), pintu clear room, boss setelah relay, kunci generator |
| `Scripts/Dungeon/DungeonSession.cs` | Hook scene game, spawn player/zombie/boss/item, goal, path guide, HUD |
| `Scripts/Dungeon/DungeonDebugView.cs` | Panel seed + F10 detail + peta grid real-time, hotkey |
| `Scripts/Dungeon/DungeonGizmos.cs` | Gizmos Scene View (leaf, room, corridor, critical path, spawn) |
| `Scripts/Dungeon/DungeonExperiments.cs` | F9 → `ExperimentResults.md` |
| `Scripts/Dungeon/DungeonProfile.cs` + `Resources/Dungeon/Dungeon_Profile.asset` | Semua parameter (diedit di Inspector) |
| `Editor/DungeonEditorTools.cs` | Menu **Tactical Outpost → Dungeon**: Select Profile, Preview in Scene, Print ASCII Map, Capture Mode Preview |

## Asset yang dipakai (semua sudah ada di project)

| Elemen | Prefab |
|---|---|
| Floor | `Road_set_v1_s_floor` (1 per cell, di-scale ke tileSize 3,5 m) |
| Wall | `Concrete_fence_v1_s1` (panel beton solid) + `Concrete_fence_v1_column` di sudut |
| Goal | Generator + lampu merah dari scene PCG (berubah hijau saat goal tercapai) |
| Lampu | Lampu jalan per room, `BurningBarrel_Military` di corridor/dead-end |
| Props cover | Obstacle/dressing mode PCG yang footprint-nya muat di 1 cell |
| Enemy / item | Zombie walker/runner/skinless/bloater + boss dari EnemyCatalog; ammo/medkit dari DropManager |

## Zombie per room menurut jarak

Room diurutkan dari jarak BFS ke Start lalu dibagi rata ke empat tingkat: **3 → 5 → 7 → 9 zombie** (room Start/safe room = 0; Random Walk memakai 4 pita jarak BFS). Penjaga relay ikut dihitung dalam kuota room. Jenisnya divariasikan: tiap room mendahulukan jenis yang belum dipakai, makin jauh makin condong ke runner/skinless/bloater (`heavyBiasFar`), dan bloater tidak muncul di tingkat pertama. Boss dipesan lebih dulu di Goal room. Total per dungeon 60×40 sekitar 35–63 zombie + boss (Eksperimen 11). Atur di `zombieTiers` / `zombiesByDistance`.

## Isi room (props)

Tiap room berisi props cover di tengah (2–3, makin luas makin banyak, `coverCellsPerExtra`), dekorasi yang menempel dinding (2–3: barrel, pallet, dumpster, barrier; `wallPropsPerRoom` / `wallCellsPerExtra`), dan peluang 75% satu **props besar** menempel dinding (cargo container dan props lain yang lebih panjang dari 1 cell; room ≥ 60 cell bisa dapat 2, `largePropChance`). Props besar menempati deretan 2–3 cell tepi room, sisi panjangnya sejajar dinding. Jenis props dalam satu room dibuat beragam (jenis yang belum dipakai di room itu didahulukan). Rata-rata ±5,5 props per room, ±0,9 di antaranya props besar. Semua props tidak di mulut pintu/critical path/safe zone/goal, sisi dalamnya tetap lantai, dan tidak bersebelahan dengan props lain sehingga room tidak terputus (80/80 seed valid pada percobaan pertama).

**Ikon minimap:** power relay = bulatan magenta (hijau setelah aktif), generator = ikon generator Mini Project berwarna emas (terkunci) → cyan (siap) → hijau (menyala). Ukurannya ±1,85× dan ±2,3× ikon pickup (lebih kecil di peta besar), ada panah di tepi radar bila di luar jangkauan. Ukuran: `mapRelayIconSize` / `mapGeneratorIconSize`. Ikon medkit/ammo/zombie dibuat konsisten: aset radar membuat ikon dengan rotasi 3D objek saat spawn (yaw acak → ikon gepeng), jadi di mode Dungeon ikon diratakan kembali tiap 0,2 s dan ikon ammo diperbesar sedikit (`mapAmmoIconSize`).

## Pencahayaan

Lampu pengisi tanpa mesh (komponen Light, tanpa bayangan): satu per kotak 4×4 cell di setiap room (ketinggian 5 m) dan tiap 5 cell di corridor, ditambah lampu jalan dan burning barrel yang sudah ada. Cahaya bulan ×2,2 dan ambient ×2,5 hanya selama mode Dungeon (scene PCG tidak diubah). Atur di bagian **Pencahayaan dungeon** pada profile.

## Constraint

| Jenis | Aturan |
|---|---|
| Hard | Room ≥ 2, Start & Goal di floor, Start–Goal terhubung (BFS), semua room terjangkau, enemy/item di floor valid, enemy ≥ 8 cell (BFS) dari Start, **semua relay terjangkau tanpa melewati Goal** |
| Soft | Start–Goal ≥ 25 cell, enemy ≤ 1/10 floor, item tersebar (spacing 4), room tidak terlalu kecil, ada cabang/loop, jumlah relay sesuai, ≥ 1 relay di luar critical path |
| Placement | Props: tidak di critical path ± 1 cell, tidak di mulut pintu, cover: 3×3 sekitar kosong, dekorasi tepi: sisi dalam tetap lantai, tidak di safe zone/goal. Enemy: bukan safe room, tidak dekat start, tidak menumpuk. Boss: hanya Goal room. Item: dead-end → room tanpa Start/Goal |

## Hotkey (mode Dungeon)

| Tombol | Fungsi |
|---|---|
| E | Aktifkan power relay (di dekat relay) |
| F10 | Panel detail + peta grid (leaf BSP, room, corridor, critical path, start, goal, enemy, item, props, posisi player) |
| F5 | Path guide di lantai (BFS dari posisi player ke goal) on/off |
| F6 / F7 | Dungeon baru (seed baru) / ulangi seed yang sama |
| F9 | Eksperimen otomatis → `Docs/Dungeon/ExperimentResults.md` |
| F11 | Demo: teleport ke relay belum aktif terdekat; bila semua aktif, ke dekat goal |
| F8 → F4 | Debug bawaan game: bunuh semua zombie (untuk demo cepat clear room / boss) |
| F12 | Ganti BSP ↔ Random Walk (seed sama) |
| M | Minimap besar (bawaan game) |
| ENTER | Setelah goal: dungeon baru. Setelah gugur: ulangi seed yang sama |

Parameter: **Tactical Outpost → Dungeon → Select Profile**. Untuk demo reproducibility matikan `Use Random Seed` dan isi `Seed`.

## Jawaban pertanyaan analisis (data dari `ExperimentResults.md`)

1. **Random vs procedural generation.** Random hanya mengambil nilai acak tanpa aturan. Procedural = random + algoritma + rule + validasi. Di mode ini random hanya menentukan posisi split BSP, ukuran room, arah L-corridor; BSP, constraint, dan BFS yang menjamin hasilnya dungeon yang bisa dimainkan.
2. **Kenapa tidak cukup random.** Enemy acak tanpa rule bisa muncul di wall atau di sebelah Start. Di Eksperimen 5 (20 enemy) ada 109 kandidat ditolak `NotFloor`, 10 `NearStart`, 4 `SafeRoom`. Tanpa rule, semua kandidat itu akan dipakai.
3. **Kenapa perlu seed.** Supaya level bisa direproduksi untuk debug, demo, dan membandingkan parameter. Seed 500 dijalankan 3 kali menghasilkan hash `459708900` yang sama persis. Semua random memakai `System.Random` per subsistem, jadi tembakan player tidak menggeser hasil generation.
4. **Fungsi grid.** Representasi diskret level (`TileType[x,y]`) yang mudah diproses algoritma (BSP, BFS, constraint), lalu dipetakan ke dunia lewat `GridToWorld` (1 cell = 3,5 m).
5. **Kenapa BSP cocok untuk room-based.** BSP membagi ruang menjadi area yang tidak saling tumpang tindih, jadi room otomatis tidak bertabrakan. Struktur pohonnya juga memberi pasangan sibling yang tinggal disambung corridor. Hasilnya terstruktur: rata-rata 10,6 room per dungeon 60×40.
6. **Leaf.** Node BSP yang tidak dibagi lagi karena sudah ≤ maxLeafSize atau terlalu kecil untuk dibagi. Satu leaf = satu calon room. Eksperimen 2: minLeafSize 8 → 13 leaf, 16 → 5,2 leaf.
7. **Kenapa room tidak memenuhi leaf.** Supaya ada jarak antar-room untuk dinding dan corridor, dan bentuknya bervariasi. Eksperimen 3: roomMargin 1/2/4 → jarak room ke tepi leaf 1,4 / 2,2 / 4,1 cell. Margin 4 membuat room menyusut (5,8 room, floor 13,7%).
8. **Fungsi corridor.** Menghubungkan room supaya Goal dan semua area bisa dicapai.
9. **Kenapa L-corridor mudah.** Cukup dua garis lurus (horizontal lalu vertical), selalu tersambung di grid, dan tidak perlu pathfinding.
10. **Hard constraint.** Aturan yang wajib benar. Kalau dilanggar level ditolak: repair atau generate ulang (lihat daftar di atas).
11. **Soft constraint.** Preferensi kualitas. Kalau dilanggar, level tetap dipakai dan hanya dilaporkan. Contoh: Start–Goal ≥ 25 cell, ada loop.
12. **Kenapa enemy tidak dekat Start.** Supaya player tidak langsung diserang saat spawn dan punya waktu orientasi. Di sini jaraknya dihitung BFS (≥ 8 cell = 28 m jalan), bukan jarak lurus, sehingga enemy di balik dinding tetap ditolak.
13. **Kenapa perlu validasi.** Layout yang terlihat benar bisa saja terputus. Eksperimen 8: dengan 30% corridor sengaja dilewati, 16 corridor hilang dan 4 kali repair diperlukan. Tanpa validasi, dungeon itu tidak bisa diselesaikan.
14. **BFS memvalidasi dungeon.** Flood fill dari Start ke semua floor (props dihitung penghalang). Goal, setiap room, setiap enemy dan item harus punya jarak ≥ 0. Jarak BFS juga dipakai untuk strategi Goal C, constraint enemy, critical path, dan path guide player.
15. **PCG + NavMesh + AI.** Level baru langsung bisa dipakai AI: NavMesh di-bake dari hasil build, validasi jalur start → goal ulang dengan NavMesh (contoh: "OK start → goal 247 m"), dan zombie EnemyBrain asli bisa patroli/mengejar di layout yang belum pernah ada sebelumnya tanpa authoring manual.

## Objektif — hasil eksperimen (F9)

- **Eksperimen 9 (jumlah relay):** rute Start → relay → Goal rata-rata 1,00× / 1,21× / 1,48× / 2,17× dari jalur langsung untuk 0 / 1 / 3 / 5 relay; semua 5/5 valid. Jadi relay memaksa eksplorasi tanpa membuat dungeon tidak valid.
- **Eksperimen 10 (clear room):** dengan relay 3, 4–6 combat room dan 7–17 pintu per dungeon; mayoritas zombie berada di combat room (contoh seed 505: 10 di room, 1 di corridor).
- Diuji di Play Mode (seed 685125): room tersegel saat masuk → relay menolak "CLEAR THE ROOM FIRST" → setelah bersih relay 1/3, 2/3, 3/3 → `BOSS AWAKE` → generator menolak "ELIMINATE THE BOSS FIRST" → boss mati → `POWER RESTORED` (relays 3/3, rooms cleared 7).

## Batasan

- Collider mesh non-Read/Write di-bake ke NavMesh sebagai kotak (sama seperti mode PCG).
- Belum diuji di standalone build.
- Pertarungan pada tes Play Mode memakai debug kill-all (F8 → F4), bukan tembak-menembak penuh.
