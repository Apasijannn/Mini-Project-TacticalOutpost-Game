# Tactical Outpost — Gameplay Presentation Brief

Dokumen ini merangkum implementasi gameplay Tactical Outpost yang ada di project saat ini. Angka wave dan boss mengacu pada konfigurasi scene gameplay; angka atribut enemy dan senjata mengacu pada config/prefab yang dipakai runtime.

## Elevator pitch

**Tactical Outpost** adalah game FPS survival single-player. Player mempertahankan satu generator dari serangan zombie yang datang melalui tiga jalur. Ada tiga wave, dengan boss muncul di wave terakhir. Zombie bisa mengejar player atau menyerang generator berdasarkan persepsi dan utility score.

## Kondisi menang dan kalah

- **Menang:** seluruh musuh di wave terakhir mati.
- **Kalah:** HP player habis atau HP generator mencapai nol.
- Setelah mati atau generator hancur, player bisa menekan **Enter** untuk memulai ulang gameplay.
- Generator adalah objective tunggal dengan **1000 HP**. Tembakan player tidak merusaknya.
- Script generator memiliki method `Repair()`, tetapi saat ini tidak ada gameplay call yang mengaktifkan repair. Jadi belum ada mekanik repair generator untuk player.

## HP dan damage melee

Damage di tabel adalah damage **per hit yang berhasil**. Cooldown menunjukkan jeda antarserangan; angka damage bukan DPS.

| Entity | HP | Damage ke player / hit | Damage ke generator / hit | Cooldown | Jarak serang | Jalan / lari |
|---|---:|---:|---:|---:|---:|---:|
| Player | 100 | — | — | — | — | 4,2 / 6,6 m/s saat sprint |
| Generator | 1000 | — | — | — | — | — |
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
- Alokasi target global memiliki kuota normal **ceil(50% × zombie hidup dengan AI aktif)**, termasuk boss dan sisa wave sebelumnya. Contoh: 24 zombie menyediakan 12 slot; 5 zombie menyediakan 3 slot. Kuota dihitung ulang saat jumlah zombie berubah dan tidak memaksa zombie yang belum mendeteksi player untuk mengejar. Slot mengikuti antrean **FIFO**: kontak yang lebih dahulu masuk mempertahankan urutan; tembakan baru tidak merebut slot yang sudah diisi. Bila beberapa kontak pertama muncul bersamaan, yang tertembak, mendengar kontak, lalu yang terdekat didahulukan. Zombie yang menunggu tetap bergerak menyerang generator.
- Zombie yang terkena tembakan memberi alert ke zombie lain dalam radius **8 m**. Suara tembakan dari posisi player juga tetap terdeteksi melalui radius noise senjata dan hearing multiplier masing-masing varian. Kontak suara menaikkan minat mengejar selama 2 detik, kemudian mengikuti ingatan biasa; zombie yang terkena tembakan mempertahankan minat selama memoryTime (normal 8 detik, boss 14 detik). Alert baru masuk antrean dan tidak melewati kontak yang lebih dahulu menunggu.
- Pengecualian jarak melee: jika player terlihat, beda tinggi kurang dari 1,8 m, dan jaraknya tidak melebihi **attackRange + 0,6 m** (normal 2,3 m; boss 3,1 m), zombie memprioritaskan player walaupun kuota sudah penuh, termasuk yang masih berjalan menuju generator. Zombie dekat ikut dihitung dalam kuota, tetapi jumlahnya boleh melampaui 50% untuk sementara. Setelah player keluar dari radius itu, zombie yang tidak mendapat slot FIFO kembali ke generator; serangan tertunda ke player dibatalkan. Alokasi diperiksa sekitar setiap 0,15 detik dan dihitung ulang setelah spawn, kematian, atau AI dinonaktifkan.
- Zombie yang sudah mencapai jangkauan serang generator terkunci pada generator tersebut. Tembakan, noise, dan slot player yang kosong tidak mengalihkan komitmen ini. Player yang masuk jarak melee tetap dapat menarik reaksinya sementara; setelah player menjauh, zombie tersebut kembali ke generator walaupun slot pengejar player tersedia.
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

## Pembaruan menu dan cinematic — 21 Oktober 2026

- Main menu menampilkan judul **BLACKOUT**, aerial view camp dengan gerakan kamera perlahan, serta 30 zombie dekoratif yang berjalan menuju generator. Mereka tidak memulai wave, menimbulkan damage, atau masuk ke daftar musuh gameplay.
- Tombol Play di Unity Editor selalu memulai `TacticalOutpost_Menu`, melalui `OutpostPlayEntry` di folder Editor. Pengaturan start scene dipasang saat Editor dimuat dan sebelum masuk Play, termasuk bila pengaturannya sempat kosong. Build tetap memulai scene menu yang sudah berada di urutan pertama Build Settings. Scene yang sedang diedit tidak ditulis ulang. Pemeriksaan tersedia di `Docs/Integration/PlayEntryChecks.json`.
- **Settings** berisi Master Volume, Mouse Sensitivity, Field of View, Head Bob, Fullscreen, dan Quality. Apply menyimpan pengaturan; meninggalkan halaman tanpa Apply membatalkan preview volume.
- **Controls** menampilkan diagram keyboard, mouse, dan binding aktual. **Field Manual** berisi briefing, empat varian zombie normal, tiga varian boss, AK-47, dan M1911. Model dapat diputar dengan drag mouse dan berputar otomatis setelah tiga detik tidak disentuh, termasuk saat pause. Statistik diambil langsung dari config musuh dan prefab senjata. Boss cadangan diberi label karena tidak digunakan wave saat ini.
- Kematian player menghentikan wave dan input, menampilkan kamera first person tumbang selama sekitar 1,5 detik, slow motion 0,25x, grayscale, blur, serta vignette. Waktu kembali normal secara bertahap setelah tumbang; zombie di dekat player tetap memainkan serangan ke tubuh yang sudah jatuh tanpa damage tambahan.
- Kehancuran generator memulai cinematic drone selama 12 detik dengan orbit, camera shake, War FX untuk ledakan, dan Free Fire VFX untuk api. Ledakan generator terjadi sekitar detik 1,2, diikuti Power Station yang terhubung pipa pada detik 1,7 (selisih sekitar 0,5 detik), kemudian seluruh kelompok tangki serentak pada detik 2,2. Generator dan Power Station masing-masing terpecah menjadi 84 bagian. Pipe yang terpasang dan jalur penghubung runtuh. Panel tembok penahan kilang ikut pecah akibat shockwave, tanpa ledakan api tersendiri; pagar perimeter camp tidak menjadi target kehancuran. Setelah cinematic selesai, layar BLACKOUT muncul dan Enter memulai sesi baru.
- Pecahan mesh merupakan potongan permukaan yang disiapkan di Editor dengan collider sederhana. Ini belum berupa fracture solid dengan penutup pada setiap permukaan potong. Kehancuran hanya terjadi saat runtime; layout industrial yang tersimpan tidak diubah.
- Hasil pemeriksaan tersedia di [CinematicMenuChecks](Docs/Integration/CinematicMenuChecks.json) dan [BlackoutEndingChecks](Docs/Integration/BlackoutEndingChecks.json). Pemeriksaan dilakukan di Editor Play Mode; standalone build belum diperiksa.

## Pembaruan HUD — 21 Oktober 2026

- HUD menggunakan font, panel gelap, dan aksen amber yang sama dengan menu BLACKOUT. Panel senjata memiliki garis orange di kanan. Minimap kecil dan tactical map tetap berbentuk lingkaran, dengan ring amber tipis mengikuti tepi lingkaran dan tanpa panel kotak di belakangnya.
- Notifikasi, peringatan wave/boss, dan teks akhir tampil bertahap dengan suara input elektronik. Pengetikan berhenti saat pause; volume mengikuti Master Volume. Angka health, ammo, dan countdown tetap diperbarui langsung.
- Tidak ada tulisan **RELOADING** selama animasi reload. Petunjuk **Press R to reload** hanya muncul saat magasin kosong dan masih memiliki amunisi cadangan.
- Pemeriksaan HUD, warna peringatan boss, pergantian senjata, health bar, serta prioritas M/Esc tercatat di [HudStyleChecks](Docs/Integration/HudStyleChecks.json).

Pemeriksaan kuota target, alert di sekitar zombie yang tertembak, pengecualian melee, pengalihan kembali ke generator, penghitungan ulang setelah kematian, serta reset HP tersedia di [TargetQuotaChecks](Docs/Integration/TargetQuotaChecks.json). Hasil cinematic mencatat selisih ledakan gedung sekitar 0,50 detik setelah generator.

## Audio ledakan cinematic — 21 Oktober 2026

- Sumber audio berasal dari enam MP3 Dragon Studio yang diberikan pengguna di folder `xplosion`. Salinan sumber dipertahankan dalam `Assets/TacticalOutpost/Gameplay/Audio/Explosions/Sources`; nama asli tetap tersedia.
- Generator memakai kombinasi Massive Explosion dan Nuclear Explosion yang difilter untuk gemuruh rendah, dengan durasi sekitar 8,48 detik. Gedung memakai Loud Explosion Sound dan Massive Explosion yang difilter, sekitar 4,28 detik. Kelompok tangki memakai Explosion Sound Effect dan Explosion FX, sekitar 2,74 detik.
- Audio dipicu bersamaan dengan ledakan masing-masing: generator pada detik 1,2, gedung pada detik 1,7, dan kelompok tangki pada detik 2,2. Seluruh tangki memakai satu layer suara gabungan agar tidak menumpuk empat suara penuh sekaligus.
- Ketiga mix berupa WAV mono 48 kHz dengan peak maksimum 0,7, fade singkat pada tepi clip, dan gain runtime berturut-turut 0,55 / 0,40 / 0,25. Spatial blend 0,65, min distance 30 m, max distance 200 m, dan Doppler dimatikan agar orbit drone tidak mengubah pitch. Master Volume dan pause audio tetap berlaku.
- Data durasi dan peak gabungan tersedia di [ExplosionAudioMix](Docs/Integration/ExplosionAudioMix.json). Preview audio terpisah tersedia di `Temp/ExplosionMixPreview.wav`; preview ini tidak mencakup ambience atau suara api dari scene.
