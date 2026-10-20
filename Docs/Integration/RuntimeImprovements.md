# Perbaikan runtime dan permintaan lanjutan — 20 Oktober 2026

## Perubahan tersimpan

- Recoil memakai pola tembakan, smoothing, batas pitch/yaw dan recovery tertunda. ADS dan crouch mengurangi recoil. Weapon switch menghapus offset lama. Crosshair merespons recoil. Flexible Recoil System tidak dipasang; perbaikan memakai sistem yang sudah ada.
- Boss visualScale 2.3 (sebelumnya 2), sekitar 15% lebih besar. Root tetap scale 1; collider dan agent mengikuti ukuran visual. Boss memakai agent type dan baked NavMesh tersendiri (radius 0.9 m, height 4.4 m). Normal agent tetap memakai konfigurasi lama.
- Spawn/path queries memakai agent type yang sesuai. Boss tidak lagi memilih player yang lokasinya belum diketahui hanya karena bias skor target.
- Marker hidup mengikuti zombie secara real time: normal kuning, boss merah, ukuran kecil, indikator tepi untuk posisi di luar cakupan. M membuka/menutup map besar; Esc menutup map dulu dan mempertahankan cursor state.
- Wave interval default 60 detik, mulai dihitung saat banner wave dimulai. Sisa enemy tidak menghalangi wave berikutnya dan tetap dihitung. Setelah wave final semua survivor harus dikalahkan untuk Victory. Total 10 + 14 + 18 normal dan satu boss. READY awal 6 detik; HUD DEAD menggantikan GUGUR.
- PlayerStart di tengah jalan depan generator, antara dua tower depan: x=-4.2,z=-8, y mengikuti sampel NavMesh, menghadap generator. Koordinat lengkap: PlayerSpawn.json.
- Tambahan terpisah di luar camp: 48 dry trees dan 36 scanned stumps, variasi prefab, yaw dan scale; penempatan menolak interior, approach corridors, overlap bounds dan lereng curam. Manifest: AdditionalExteriorNature.json. Kedua NavMesh sudah di-bake ulang setelah tambahan ini.
- DBK Mesh Combine tidak lagi memakai Shift+D; menu DBK/Mesh Combine tetap tersedia. Mengatasi dialog Shortcut Conflict ketika sprint + kanan. Nama menu terkompilasi diperiksa dengan reflection dan Shift+D dicoba langsung di Editor tanpa dialog.
- FpsController.Move melewati gerak ketika CharacterController dinonaktifkan, sekaligus menghapus velocity/sprint lama. Ini menghilangkan spam Move called on inactive controller selama debug/teleport.

## Hasil pemeriksaan

| Pemeriksaan | Hasil |
|---|---|
| Recoil otomatis, ammo, recovery, ADS, pistol, weapon switch | PASS; RecoilRuntimeChecks.json |
| Tiga boss bergerak dengan agent type/clearance sesuai dan 4/4 enemy terdaftar radar | PASS; BossRadarRuntimeChecks.json |
| M, Esc, cursor state, buka ulang | PASS; MinimapControlChecks.json |
| 3 boss dan 3 normal dari tiga lane mencapai generator dan menyerang | PASS; NavigationTravelChecks.json |
| Wave berikutnya mulai dengan 10 lalu 24 survivor | PASS; TimedWaveChecks.json |
| Player death, generator defeat, Enter restart | PASS; RestartRuntimeChecks.json |
| CharacterController dinonaktifkan: posisi tetap, tanpa error | PASS; DisabledControllerChecks.json |
| Frame runtime benar-benar bertambah | PASS; frameCount berubah 4819 ke 4828 |

Tes wave dipercepat hanya pada runtime; scene tetap interval 60 detik. Tes navigation memakai player sementara di luar area, sehingga sempat terlihat langit pada screenshot pengguna; posisi uji sudah dibersihkan dan regular spawn dipulihkan. Screenshots final berada di Assets/TacticalOutpost/Documentation/Debug.

## Perlindungan environment dan backup

Backup source lengkap: C:/Users/Faiz/Documents/TacticalOutpostAudits/20261020_211802/IndustrialProject_Backup.zip.
Checkpoint sebelum perubahan gameplay: C:/Users/Faiz/Documents/TacticalOutpostAudits/20261020_220546_RecoilNavigation/GameplayBefore.zip; scene sebelum tambahan nature juga tersedia di folder ini.

Perbandingan transform mencakup 3470 object lama, tidak ada object hilang. Perubahan yang diminta: PlayerStart serta group nature baru. Satu roadblock (Road_block_v1 (18)) juga berbeda dari snapshot awal: (1.69,0.88,-9.79), yaw111.5 menjadi (1.8,0.88,-9.7), yaw105.63. Scripts perubahan gameplay/nature tidak menyentuh roadblock tersebut; perubahan Editor yang ada dibiarkan dan tidak direvert. Detail: EnvironmentPreservationChecks.json. Scene yang masih dirty tidak dipaksa disimpan agar perubahan pengguna tidak tertimpa.

## Cakupan adaptasi dan batas

RepoScriptCoverage.json diperbarui: semua 23 script runtime inti diimpor, sekarang 11 dimodifikasi, 12 tetap sama. Satu script vendor minimap, Aux_MP, dimodifikasi untuk normalisasi dan ukuran marker. Daftar komponen yang dilewatkan beserta alasannya ada di AdaptationDifferences.md (catatan awal) dan manifest coverage.

Tidak menjalankan build executable atau soak test lama. Balancing interval 60 detik, performa jumlah enemy menumpuk, dan rasa recoil masih memerlukan playtest manusia. Enemy belum memiliki mekanik jump/climb antar permukaan NavMesh yang terpisah.

## Perbaikan gerak player lanjutan

CharacterController pada instance Player_FPS di scene gameplay tersimpan disabled, walaupun prefab sumber enabled. Instance diaktifkan dan override disimpan melalui Unity Editor API. Pembacaan ulang scene dari disk memastikan enabled=true. Tes Play Mode mengirim input Input System untuk W/A/S/D dan keempat arrow: seluruhnya menghasilkan perpindahan horizontal 0.46–0.53 meter dalam 0.25 detik. Hasil lengkap MovementRuntimeChecks.json. Posisi player dipulihkan setelah tes. Environment tidak diedit. Console pada pemeriksaan runtime: 0 error, 0 warning.

## War FX, spawn, dan sambungan gerbang — 21 Oktober 2026

- Paket resmi War FX 1.8.1 diperoleh setelah persetujuan EULA pengguna. Muzzle flash untuk AK-47 dan pistol dipasang pada muzzle yang sudah ada; pose tangan/senjata tetap. Impact permukaan memakai Concrete, enemy memakai SoftBody. Damage dan hit logic tetap. Muzzle adalah burst satu particle per lapisan selama 0.05 detik; tidak loop dan berhenti saat weapon dinonaktifkan. Impact mengikuti normal permukaan dan dibersihkan otomatis, dengan batas fallback 4 detik.
- Adaptasi URP di Gameplay/Effects/WarFX: empat prefab, tujuh material, lima alpha-mask RGBA (warna putih + alpha asli). Texture Alpha8 vendor menghasilkan RGB hitam pada shader URP standar; salinan mask diperlukan agar flash tampak. File vendor asli tidak dikonversi. Demo/editor paket baru dibuang; paket resmi tersedia di cache untuk import ulang. Setup: Tools/Integration/SetupWarFx.cs. Hasil: WarFxRuntimeChecks.json, screenshot WarFxVisible2.png. Dua senjata menembak, ammo berkurang, particle muzzle terbit, impact dibuat dan cleanup lolos.
- Screenshot tembok diperjelas pengguna sebagai sambungan ujung pagar ke tiang gerbang. Bukan object renderer yang dimatikan runtime: dua celah fisik gerbang barat diisi prefab pagar yang sama, panjang 2.508 dan 2.634 meter. Hanya panjang modul pengisi disesuaikan; tinggi, tebal, material dan posisi tower/tiang lama tetap. Sambungan masuk 1 cm ke sisi tiang/panel agar tidak menyisakan hairline. Gerbang tetap terbuka. GateConnections.json dan WestGateAfter.png.
- Spawn sebelumnya menyebar hingga sekitar 7.5 meter ke samping dan tujuh titik South berada di tanah kosong di belakang camp. Semua 21 titik sekarang staggered di atas paving yang ada: West dan North pada jalan pendekatan luar, South dipindah ke service road bagian selatan (z=-37,-35,-33). Tidak perlu menambah/menggeser jalan. South lebih dekat ke objective sehingga tekanan flank meningkat.
- Kedua NavMesh dibake ulang setelah pengisian pagar. Cek akhir 21 titik x 2 tipe agent x 9 offset jitter = 378 sampel: seluruhnya di PlayArea dan memiliki path lengkap ke generator; raycast memastikan 21/21 titik berdiri di Road_set. SpawnJitterChecks.json dan SpawnRoadAlignment.json mencatat hasil/koordinat.
- Backup sebelum pekerjaan: C:/Users/Faiz/Documents/TacticalOutpostAudits/20261021_GateAndWarFX/EnvironmentBefore.unity dan GameplayBefore.unity. FpsWeaponBefore.cs juga disimpan. GatePreservationChecks.json membandingkan transform environment lama dengan backup: tidak ada object lama hilang atau berpindah; hanya group connector baru ditambahkan.
- Link Mysterious Fog (Royalty Free) 213501 merupakan AUDIO/MUSIC, bukan visual fog. Paket ini tidak dipasang dan lighting/fog map tidak diubah. Visual fog dapat memakai setting bawaan; musik ini dapat dipertimbangkan terpisah sebagai ambience.
- Console pada cek runtime akhir: 0 error, 0 warning. Play dihentikan; timeScale=1 dan runInBackground dikembalikan false. Build standalone, profiling efek saat banyak tembakan, dan balancing jarak spawn South belum diuji panjang.

## North gate, approach walls, English UI, dan aturan wave clear — 21 Oktober 2026

- Screenshot 010659/010650 menunjukkan North gate; perbaikan sebelumnya hanya West gate. Ditambah satu tiang pasangan dan dua sambungan beton/lattice sepanjang 2.769 dan 2.779 m, memakai prefab dan material yang sama. Gerbang terbuka tanpa pintu. Bukti Play: NorthGateEastFixed.png dan NorthGateWestFixed.png di Assets/TacticalOutpost/Documentation/Debug. NorthGatePreservationChecks.json membandingkan backup dengan scene tersimpan: 3898 object lama tidak hilang atau berubah transform/active flag; hanya 8 object baru.
- Enam invisible BoxCollider tanpa Renderer membatasi kedua sisi dan ujung luar jalan pendekatan West/North. NavMeshModifier NotWalkable serta kedua NavMesh dibake ulang. ApproachWallRuntimeChecks.json: 12 ray fisik pada tinggi 1/4 m, 12 cek batas navigasi, dan 378 rute spawn jitter lolos. Jalan masuk camp tetap terbuka; spawn South tetap memakai service road dalam camp.
- Dot minimap normal naik dari multiplier 0.10 ke 0.17, boss 0.13 ke 0.21. Warna kuning/merah dipertahankan; 4/4 actor tracked, tiga varian boss bergerak dengan path lengkap. BossRadarRuntimeChecks.json dan EnemyDotsEnlarged.png.
- Teks UI gameplay menjadi English: READY, zombies incoming / Hold the line!, DEAD, OUTPOST LOST, OUTPOST SECURED, RELOADING, ammo/health feedback serta debug help. Label Inspector/comment internal tidak diterjemahkan. Boss mendapat BOSS INBOUND sebelum kedatangan, judul merah dan glow merah lembut berdenyut. Background tidak menerima raycast dan texture/sprite dibersihkan saat restart. EnglishUiRuntimeChecks.json serta BossWarningEnglish.png memverifikasi red title/glow, pulse, teks dan urutan warning sebelum boss.
- Aturan wave terbaru: setelah seluruh musuh terjadwal selesai masuk, wave berikutnya mulai saat semua enemy aktif habis ATAU timer berakhir, mana lebih dulu. Interval scene tetap 60 detik. Tes ClearWaveChecks.json mencatat transisi setelah clear 0.25 detik, tidak melewati jeda antarmusuh atau boss yang belum masuk. TimedWaveChecks.json tetap 10/14/19 actor dan lanjut dengan 10/24 survivor, final victory setelah semua habis. OnWaveCleared dipanggil satu kali hanya saat benar-benar clear.
- Death/generator defeat dan Enter restart lolos RestartRuntimeChecks.json. Pemeriksaan runtime akhir Console: 0 error, 0 warning, compilationFailed=false. Play dihentikan, timeScale=1, runInBackground=false, active scene Solo. Scene dirty yang sudah ada tidak dipaksa disimpan.
- Backup checkpoint: C:/Users/Faiz/Documents/TacticalOutpostAudits/20261021_NorthGateLaneWalls. Coverage per-file diperbarui: 23/23 core diimpor; 11 dimodifikasi, 12 sama. Belum menjalankan standalone build, soak test lama, atau profiling wave bertumpuk.

## Jumlah wave dan boss per gerbang — 21 Oktober 2026

- Jumlah zombie biasa dinaikkan dari 10/14/18 menjadi 24/36/48, disimpan pada WaveDirector scene gameplay lewat Editor API. Default code mengikuti data scene. Interval tetap 60 detik; clear lebih cepat tetap lanjut.
- Wave dengan boss sekarang membuat satu boss pada setiap lane Enemy: West, North, South. Wave 3 berisi 48 normal + 3 boss (51 total). Boss memakai variant dan ukuran yang sebelumnya dipilih. Red warning menjadi BOSSES INBOUND dan tetap mendahului kedatangan boss.
- TimedWaveChecks.json: wave 24/36/51, total 111 actor, 3 boss pada lane berbeda (1/5/3), survivor 24/60 terbawa, victory setelah semua habis. ClearWaveChecks.json lolos dan menjaga ketiga boss yang masih dijadwalkan. EnglishUiRuntimeChecks.json memverifikasi warning plural dan glow.
- Backup sebelum edit: C:/Users/Faiz/Documents/TacticalOutpostAudits/20261021_MoreZombies. Environment dan posisi spawn tidak diedit. Console akhir 0 error/0 warning. Play dihentikan. Belum mengukur performa sesi panjang atau menyeimbangkan ammo/health terhadap jumlah baru.

## Menu sementara dan marker minimap — 21 Oktober 2026

- Strategic Warfare Sci-fi UI Starter Pack 391120 versi 1.0 ditambahkan ke akun setelah pengguna mengizinkan Accept EULA. Cache paket resmi Studio Ken O tersedia untuk import ulang. Hanya atlas PNG beserta metadata GUID aslinya yang diimpor ke Assets/ThirdParty/StrategicWarfareUI/Textures; tidak mengimpor demo scene, PSD, scripts vendor atau mengganti HUD gameplay. Sprite UI Starter Pack_1 dipakai tombol dengan tint Primary biru dan Secondary abu-abu seperti prefab artist. Importer max4096 untuk keterbacaan.
- GameMenu.cs membuat main menu START / EXIT dan pause RESUME / EXIT. Exit dari pause kembali ke main menu dan memulai sesi baru ketika Start dipilih lagi; Exit utama menutup aplikasi (di Editor keluar Play). Pause membekukan Time.timeScale, movement/weapon input dan AudioListener. Resume menunda input satu frame agar klik menu tidak menembakkan senjata.
- Scene baru Assets/Scenes/Gameplay/TacticalOutpost_Menu.unity menjadi urutan pertama Build Settings serta Play Mode start scene. Gameplay memiliki GameMenu di systems root; map Solo tetap terpisah. Esc pertama menutup large map bila terbuka, Esc berikutnya pause; Esc saat pause melanjutkan. Tidak ada tombol/options lain.
- Marker musuh disesuaikan menurut mode: minimap kecil normal 0.24 / boss 0.29, large map normal 0.20 / boss 0.245. Arrow kecil 0.42 (sebelumnya 0.25), large 0.32. Vendor Aux_MP menerima ArrowSizeMultiplier dan memperbarui skala rendered marker setiap frame agar perpindahan mode langsung diterapkan; marker non-enemy mempertahankan default 1.
- MenuRuntimeChecks.json lolos: opsi dan sprite benar, UI Input System actions tersedia, Start/Resume/Exit, cursor, pause clock/movement/audio, prioritas Esc map, serta sesi baru. MainMenu.png dan PauseMenu.png menunjukkan hasil runtime. BossRadarRuntimeChecks.json memeriksa dot sesuai mode, rendered arrow scale kecil, empat marker tracked dan tiga varian boss bergerak. SmallMapMarkersLatest.png dan LargeMapMarkersLatest.png menunjukkan kedua mode.
- Tes wave diulang dengan jumlah 24/36/48 + tiga boss: TimedWaveChecks.json dan ClearWaveChecks.json lolos. Ditemukan race AI ketika enemy mati sebelum Start pada frame kelahirannya: Start kini melewati health.IsDead, Update tidak memanggil agent saat disabled/off NavMesh. Sesudah perbaikan, tes wave/clear/menu/radar tidak menghasilkan error. Console akhir ground truth: 0 error/0 warning.
- Backup menu: C:/Users/Faiz/Documents/TacticalOutpostAudits/20261021_MenuAndMarkers; termasuk copy scene gameplay authoring sebelum menambah menu. Environment Solo dan Gallery yang dirty tidak dipaksa disimpan. Play dihentikan, timeScale=1, AudioListener.pause=false, runInBackground=false. Belum menjalankan standalone build, menguji Application.Quit di executable, atau profiling/balancing sesi panjang dengan jumlah baru.
