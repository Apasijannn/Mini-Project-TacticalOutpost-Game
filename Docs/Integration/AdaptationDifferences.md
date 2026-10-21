# Catatan komponen dan perubahan adaptasi

> Update runtime terbaru: [RuntimeImprovements.md](RuntimeImprovements.md). Angka dan status pemeriksaan di bawah merupakan catatan integrasi awal; coverage terkini: 9 dari 23 script inti dimodifikasi, plus Aux_MP vendor.


Snapshot sumber: `Apasijannn/Mini-Project-TacticalOutpost-Game`, commit `fe2f658b2dfccf9541b07e69fbdcbee7d1823cf1`, 20 Oktober 2026 pukul 19:21:57 WIB.

Catatan ini berdasarkan manifest import, perbandingan isi file terhadap clone sumber, scripts authoring, dan konfigurasi scene yang disimpan. Ini bukan laporan hasil Play Mode.

## 1. Ringkasan cakupan

- Semua **23 file C# runtime inti** di `Assets/Outpost/Scripts` dibawa. Tidak ada file runtime inti dari folder tersebut yang dilewatkan.
- Dari 23 file itu, **18 isi scripts tetap sama**, **5 file diubah**. Salah satu perubahan adalah pemisahan sembilan class marker ke file masing-masing; class-nya tetap tersedia.
- Sembilan scripts vendor minimap juga dibawa dengan isi yang sama, termasuk patch radar milik temanmu.
- Total import awal: **201 assets/files** beserta `.meta` sumber, tanpa overwrite file lama. Metadata folder baru dibuat oleh Unity; folder assets environment lama tidak dipindah.
- Dari 201 payload tersebut, lima file C# berbeda secara isi. RenderTexture minimap berbeda hash byte setelah penyimpanan Unity, tetapi perbandingan teks tidak menunjukkan perbedaan properti: perubahan format/newline, bukan fitur baru.
- Scene environment kita tetap menjadi map utama. Scene gameplay baru menyimpan player, objective proxy, spawn markers, navigation dan systems.

## 2. Komponen repo yang tidak di-import

| Bagian sumber | Keputusan | Alasan |
|---|---|---|
| `Assets/Scenes/TacticalOutpost.unity` | Tidak dimasukkan sebagai scene penuh | Berisi layout dan susunan environment temanmu. Menggantikan map kita akan bertentangan dengan keputusan tim. Nilai gameplay penting diambil secara selektif, termasuk 32 player overrides. |
| Spawn markers sumber: 18 West + 18 East | Susunan 36 titik sumber tidak disalin | Koordinatnya milik map sumber; dibuat ulang untuk jalur barat/utara/selatan kita. |
| NavMesh/baked navigation milik map sumber | Tidak dipakai | Data navigasi bergantung pada geometri map. Dibake terpisah dari collider map industrial kita. |
| Generator visual sumber dan delapan attack points sumber | Tidak disalin sebagai objek | Objective memakai generator besar berlampu merah yang sudah ada. Collider gameplay dan attack points mengikuti ukuran/akses generator kita. |
| Layout buildings, containers, roads, fences, terrain, lighting, sky, dressing/VFX milik scene sumber | Tidak disalin ke environment | Menjaga susunan industrial, lampu tower/generator, barrel fire, terrain dan assets kita. Model/material karakter, enemy, senjata dan pickups yang menjadi dependency tetap di-import. |
| `Assets/Editor/Outpost/OutpostBuilder.*` | Tidak di-import | Builder dapat membuat ulang scene, material, meshes, lighting, marker, navigation dan Build Settings. Tidak diperlukan untuk menjalankan runtime yang sudah dipindahkan. Prinsip wiring yang relevan dibaca dan diterapkan lewat tools adaptasi sendiri. |
| `OutpostWatcher`, `OutpostCompileLogger` | Tidak di-import | Watcher bisa menjalankan builder otomatis dari request file. Logger khusus workflow sumber tidak dibutuhkan untuk runtime. |
| `OutpostPlayTest`, `OutpostWaveTest`, `OutpostProbe` | Tidak di-import | Diagnostics/test helpers mengasumsikan scene/hierarchy sumber; beberapa dapat membuka scene atau masuk Play Mode. Tidak menjadi bagian mekanik game. |
| `ProjectSettings` dan manifest `Packages` repo secara keseluruhan | Tidak disalin | Menghindari overwrite pengaturan rendering, input, physics, packages dan build milik project kita. Layers yang diperlukan ditambahkan secara selektif; Build Settings dibuat untuk scene kita. |
| Vendor vehicle scripts `RetroStyleGames/LastGuns/Base/Scripts` | Tidak di-import | Bike/buggy controller, vehicle selection, third-person camera dan input demo tidak menjadi dependency player FPS atau zombie-wave loop yang diaudit. Kendaraan dekorasi di map kita tetap ada. |
| Handgun demo `SimpleShoot.cs` | Tidak di-import | Senjata game memakai `FpsWeapon`; demo ini akan menjadi jalur tembakan/input yang berbeda. Model, material dan dependency pistol yang diperlukan tetap dibawa. |
| Skinless Zombie `Demo Scripts/*` | Tidak di-import | Demo animation selector, UI dan camera bukan AI game. Model/animasi yang dipakai enemy tetap dibawa melalui controller game. |
| Insurgent `Editor/WelcomeWindow.cs` | Tidak di-import | Jendela onboarding vendor; tidak diperlukan untuk model tangan/player. |
| `SampleScene`, vendor demo scenes dan tutorial/readme dari repo | Tidak di-import | Tidak diperlukan untuk gameplay industrial; project lokal sudah memiliki template/tutorial sendiri. |
| Assets lain di luar dependency closure | Tidak di-import secara massal | Tidak direferensikan oleh gameplay yang dipilih. Termasuk asset cadangan/demo/varian lain dan knife yang tidak digunakan oleh runtime dua-slot. Ini bukan berarti semua assets tersebut rusak atau tidak berguna. |

Daftar persis 41 scripts sumber yang tidak di-import tersedia di `RepoScriptCoverage.json`. Mereka terdiri dari 24 Editor builder/diagnostics dan 17 vendor/demo scripts. Scripts tutorial yang kebetulan sudah ada di project lokal bukan hasil import baru.

## 3. Kode dibawa, tetapi komponennya tidak dipasang sebagai fitur scene

| Komponen | Status dan alasan |
|---|---|
| `EnemyDebugSpawner` | Filenya dibawa karena `WaveDirector` memakai static helper `SpawnAt`. Komponen keyboard debug F1/F2/F3/F4 tidak dipasang pada scene gameplay, sehingga tidak mencampur enemy debug yang tidak dilacak director dengan wave normal. |
| `EnvCoverPoint`, `EnvLane` | Class tersedia, tetapi titik cover dan waypoint dari scene sumber tidak dipindahkan. `EnemyBrain` sumber tidak mengonsumsi marker ini untuk tactical cover atau mengikuti waypoint. Enemy memakai NavMesh dan pemilihan target. |
| `EnvSearchlight`, `EnvFlicker`, `EnvBlink`, `EnvSpin`, `EnvSlot` | Class tetap tersedia setelah pemisahan file, tetapi tidak dipasang untuk mengganti efek/props map kita. Lampu, beam, api dan scene dressing memakai setup map yang sudah disepakati. |
| Vendor minimap helper/demo scripts | Sembilan file vendor dibawa sebagai satu paket dukungan radar. Ini tidak berarti seluruh perilaku demo vendor diaktifkan pada scene; wiring gameplay memakai `MP`, `Custom_MP`, blip dan controller yang relevan. |

## 4. Perubahan konfigurasi dan scene

| Bagian | Repo sumber | Adaptasi industrial | Alasan/dampak |
|---|---|---|---|
| Komposisi scene | Gameplay dan map berada di scene sumber | Scene `TacticalOutpost_Game` memuat environment `TacticalOutpost_Solo` secara additive | Gameplay dikelola terpisah dari environment. |
| Player start | Koordinat dan arah scene temanmu | Mengikuti `GameplayMarkers/PlayerStart` map kita, di atas permukaan navigasi, menghadap generator | Spawn player sesuai map yang dipakai. |
| Pose tangan/senjata | Sebagian nilai terbaru berada di scene instance, bukan prefab | Semua 32 player overrides diterapkan dan disimpan dalam prefab variant `Player_Industrial` | Mempertahankan pose versi scene temanmu. Nilai IK/grip tidak di-tune ulang secara sengaja. Penempatan root player kemudian disesuaikan di scene kita. |
| Enemy spawn layout | 36 titik, dua lane West/East | 21 titik: tujuh West, tujuh North, tujuh South | Mengikuti tiga pendekatan map industrial. |
| Arah spawn | Rotasi tetap 90/270 derajat menurut sisi | Mengikuti rotasi marker yang diarahkan menuju objective | Sesuai orientasi jalur map. |
| Spawn jitter | Radius acak 0,8 m | 0,6 m | Mengurangi kemungkinan spawn bergeser ke penghalang/tepi jalur. |
| PlayArea | X -61,8 sampai 61,8; Z -34,6 sampai 34,6 | X -72 sampai 72; Z -64 sampai 64 | Memuat jalur dan approach markers industrial. Ini batas spawn, bukan dinding fisik atau jaminan seluruh rectangle dapat dilalui. |
| Navigation | Baked untuk geometri sumber | Asset baru `IndustrialGameplay.asset`, collider volume 160 x 30 x 145 m, center 2,5,0, agent type 0, voxel 0,16 m | Geometri map kita; data navigation environment lama tidak ditimpa. |
| Generator objective | Generator_A di map sumber | Proxy gameplay pada generator besar berlampu merah di sekitar -5,34; 4,67; -29,11 | Visual dan lampu generator kita tidak dipindah/diubah. Proxy memakai trigger collider sesuai bounds visual. |
| Generator attack points | Delapan titik sumber | Enam titik di sisi depan generator yang dapat diakses dari jaringan jalan utama | Titik pada NavMesh island terpisah diganti. Semua 21 spawn memiliki path lengkap ke keenam titik pada pemeriksaan authoring. |
| Generator label | GENERATOR | POWER STATION | Menyesuaikan objective industrial; HP dinaikkan menjadi 1000 pada 21 Oktober 2026. |
| Minimap besar | Center 0,0,0; size 74 m | Center 2,0,0; size 100 m | Memuat map industrial dan jalur luarnya. |
| Kamera minimap | Height 24,5 m; far clip 60 m | Height 40 m; far clip 100 m | Menempatkan kamera di atas struktur industrial dan memperluas jangkauan tampak. Small size tetap 27 m. |
| Minimap culling/drop ground mask | Mask sumber | Mask gameplay juga mengecualikan layer Generator (12) | Objective proxy berupa trigger tidak menjadi permukaan pickup/render visual. Collider fisik generator asli tetap pada environment. |
| Layers | Nama sumber pada slot 8-15 | Slot kosong lokal diisi Player, Enemy, Obstacle, Cover, Generator, Ground, Marker, Fence | Menjaga kompatibilitas layer hardcoded/masks source. Tidak memindahkan massal semua objek environment ke layer baru. |
| Build Settings | Entry scene sumber | Gameplay entry dan environment lokal ditambahkan; scene lokal lain dipertahankan | Memungkinkan additive loading dan restart. |
| Editor Play Mode start | Workflow sumber | Workstation ini diarahkan ke gameplay entry | Play menjalankan integrasi baru. Pengaturan workstation ini tidak menggantikan instruksi tim untuk membuka scene gameplay. |

## 5. Perubahan logic dan mekanik yang memang dilakukan

### `WaveDirector.cs`

- Mengumpulkan semua enemy spawn lanes, bukan hanya West/East.
- Membagi enemy secara bergiliran ke tiga lane yang tersedia.
- **Cadence berubah:** sumber melahirkan dua enemy sekaligus (satu West dan satu East), kemudian menunggu 0,45 detik. Adaptasi melahirkan satu enemy lalu menunggu 0,45 detik. Akibatnya jumlah total tetap sama, tetapi durasi keluarnya enemy kira-kira dua kali lebih panjang. Ini belum dibalancing lewat Play Mode.
- Total wave tetap **10, 14, 18 normal + satu boss pada wave terakhir**. Boss sekarang memilih satu lane dari ketiga lane, bukan hanya West/East.
- Jika catalog/varian/spawn/health prefab tidak valid, director menampilkan error setup dan menghentikan sesi. Spawn gagal tidak dilewatkan diam-diam hingga wave dianggap menang.
- Player mati sekarang juga menghentikan wave dan menghentikan waktu game. Sebelumnya death panel player terpisah dari lifecycle director.
- Tombol restart memuat scene milik komponen director, bukan `GetActiveScene()`, karena scene aktif saat bermain adalah environment untuk mempertahankan lighting.
- Teks banner diubah dari "kiri & kanan" menjadi pendekatan outpost agar sesuai tiga lane.

### `EnemyDebugSpawner.cs`

- Static `SpawnAt` yang juga dipakai wave diperketat: prefab harus ada; NavMesh harus ditemukan dalam 1,5 m; selisih elevasi maksimal 1,2 m; hasil harus di dalam PlayArea.
- Sumber dapat men-clamp posisi dan mencari NavMesh sejauh 6-8 m, lalu tetap instantiate ketika sample gagal. Adaptasi menolak spawn itu dan mengembalikan `null`.
- Dampak: spawn invalid menjadi error setup yang terlihat, bukan zombie diam di luar NavMesh. Tidak ada sistem retry/recovery spawn baru; masih perlu memastikan jitter tidak menimbulkan penolakan saat bermain.

### `FpsHud.cs`

- Hanya restart target scene yang diubah: `gameObject.scene.buildIndex`, bukan active scene.
- Crosshair, hit marker, headshot indicator, ammo/health display dan layout HUD tetap implementasi sumber.

### `MinimapController.cs`

- Angka height 24,5 m diganti field `cameraHeight` dengan default 40 m.
- Logic toggle M, transisi small/large, update radar dan lighting boost sementara tetap sumber. Center/size/clip merupakan penyesuaian scene pada tabel di atas.

### `OutpostMarkers.cs`

- Enums tetap di file asal.
- Sembilan MonoBehaviour dipindah ke file bernama sama dengan class di `Scripts/Environment/`.
- Tujuan: komponen marker baru dapat disimpan dengan referensi MonoScript biasa. Field dan perilaku class tetap; tidak membuang spawn/cover/lane class dari project.

## 6. Kode baru untuk integrasi

- `IndustrialGameSession.cs`: menunggu environment dimuat additive, mematikan walkthrough player dan navigation lama hanya selama runtime session, mengaktifkan navigation/systems gameplay, dan memilih environment sebagai active scene agar sky/fog/sun mengikuti map kita. Tidak mengedit scene environment saat Edit Mode.
- `Tools/Integration/import_gameplay.py`: import dependency selektif dengan penolakan overwrite/GUID collision dan pemeriksaan hash copy.
- `Tools/Integration/AdaptIndustrialGameplay.cs`: authoring scene gameplay dalam langkah terpisah, mempertahankan overrides player, placement spawn/objective, navigation, wiring systems dan minimap. Tool dijalankan eksplisit; bukan watcher otomatis dan bukan bagian build game.

## 7. Yang tetap dari implementasi temanmu

- `FpsBody` (IK tangan, grip, jari, badan/kaki), `FpsController`, `FpsWeapon`, `WeaponManager`, `PlayerHealth` tidak diubah isinya.
- Recoil, ADS, reload, ammo, hitscan, weapon switching dan movement masih logic sumber. Flexible Recoil System belum di-import/dipasang.
- `EnemyBrain`, enemy configs/stats, health/headshot, animasi/controller dan boss enrage tetap sumber.
- Drop chance, pickup rules, audio scripts dan clip yang dipakai tetap sumber. Referensi dihubungkan ulang ke scene kita.
- Pada adaptasi awal, jumlah wave, HP player/generator, damage, ammo, boss scale dan timing selain cadence distribusi spawn tidak diubah untuk balancing. Konfigurasi terkini tercatat di GAMEPLAY_PRESENTATION_BRIEF.md; generator kini 1000 HP dan targeting memiliki antrean FIFO dengan kuota normal 50%, pengecualian melee, serta komitmen zombie yang sudah mencapai generator.

## 8. Fitur yang memang belum ada pada runtime sumber

Ini tidak dihitung sebagai fitur yang dibuang saat adaptasi:

- Zombie jump/climb/roof traversal eksplisit; NavMesh di atap tidak otomatis membuat jalur lompat.
- Tactical cover AI yang memakai `EnvCoverPoint`, atau mengikuti `EnvLane` waypoints.
- Player repair interaction; ada API `Repair`, tetapi tidak ada interaction caller pada core yang diaudit.
- Multiplayer, save progression, skill/perk progression, pause/menu lengkap.
- Ledakan/spesial attack khusus Bloater; tipe ini pada core sumber merupakan varian stats/visual.

## 9. Batas pemeriksaan

Catatan ini mendokumentasikan apa yang di-import, dipasang dan dimodifikasi. Compile, referensi, hash preservation dan konektivitas NavMesh telah diperiksa saat authoring. Belum ada Play Mode, build atau tes runtime: perilaku tangan/senjata, combat lengkap, spawn jitter, boss clearance, minimap visual dan balancing belum dinyatakan lolos.
