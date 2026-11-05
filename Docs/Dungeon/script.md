# Naskah Video Demo — Tugas Praktikum 10: Procedural Dungeon (Kelompok Dingo)

| Pembicara | Bagian | Ketentuan yang dibahas | Durasi |
|---|---|---|---|
| P1 — Fa'iz | Pembuka, grid → scene, seed & parameter | Tugas 1, Tugas 2, Demo: seed sama, seed berbeda | ±4–5 menit |
| P2 — Zhan | BSP / Random Walk, parameter generator, validasi BFS + repair | Tugas 1, Tugas 4, Demo: ubah parameter, hasil validasi | ±4–5 menit |
| P3 — Sitti | Start/goal/enemy/item, objektif, main sampai selesai, penutup | Tugas 3, Tugas 5, Demo: lokasi valid, player start → goal | ±5–6 menit |

## Checklist ketentuan → bagian video

| Ketentuan (foto tugas) | Ditunjukkan di |
|---|---|
| 1. Layout grid (floor, wall, room, corridor) jadi scene dengan prefab modular | P1 bagian B, P2 bagian A |
| 2. Seed & parameter generator, seed tampil di layar | P1 bagian C, P2 bagian B |
| 3. Start, goal, enemy, item di floor valid + zona aman + tidak menghalangi jalur wajib | P3 bagian A |
| 4. Validasi BFS/flood fill, repair / generate ulang dengan batas percobaan | P2 bagian C |
| 5. Level bisa dimainkan start → goal, pesan saat goal tercapai, bantuan visual | P3 bagian B–C |
| Demo: seed & konfigurasi sama → layout konsisten | P1 bagian C (F7) |
| Demo: beberapa seed berbeda → variasi | P1 bagian D (F6) |
| Demo: ubah parameter + jelaskan pengaruhnya | P2 bagian B |
| Demo: hasil validasi konektivitas + lokasi start/goal/enemy/item | P2 bagian C, P3 bagian A |
| Demo: player bergerak start → goal, kondisi selesai | P3 bagian C |

## Persiapan sebelum rekam

1. Unity terbuka, minimize Unity Hub supaya tidak menutupi editor saat scene dimuat.
2. **Tactical Outpost → Dungeon → Select Profile**: `Use Random Seed` **OFF**, `Seed` = **500**, `Algorithm` = **BSP**. Catat nilai default `Max Leaf Size` dan `Room Margin`.
3. Tekan **F9** sekali di mode Dungeon untuk memperbarui `Docs/Dungeon/ExperimentResults.md` (isi room sekarang lebih banyak props, jadi angka placement dan hash di file lama sudah tidak berlaku). Buka file itu di tab terpisah.
4. Play → START OPERATION → berhenti di layar **CHOOSE YOUR OPERATION**.
5. Tombol bantu: F10 peta + panel validasi, F5 path guide, F6 seed baru, F7 ulangi seed, F11 teleport demo ke relay, F8 → F4 bunuh semua zombie (debug), F12 BSP ↔ Random Walk, M peta besar.

---

## P1 — Fa'iz: Pembuka, grid, seed (±4–5 menit)

### A. Pembuka

[Layar CHOOSE YOUR OPERATION]

"Halo, kami dari Kelompok Dingo. Untuk Tugas Praktikum 10, kami membuat Procedural Dungeon di game Tactical Outpost. Mode ini jadi kartu ketiga, MODE 03. Mini Project dan mode PCG tetap seperti sebelumnya."

"Tujuan mode ini: player mulai di Start room dan harus menyalakan generator di Goal room. Di jalan menuju generator ada room berisi zombie, power relay yang harus diaktifkan, dan boss."

[Klik kartu MODE 03]

### B. Grid dan prefab modular (Tugas 1)

[Tekan F10, tunjuk peta grid di kiri bawah]

"Generator kami bekerja di grid 60 kali 40 cell. Setiap cell bertipe Wall atau Floor, dan floor ditandai sebagai room atau corridor."

"Grid itu kemudian dibangun jadi scene Unity memakai prefab modular yang sudah ada di project. Satu cell jadi 3,5 meter: satu prefab lantai beton per cell, panel pagar beton sebagai dinding, kolom di sudut, lampu jalan, burning barrel, dan generator sebagai goal. Kami tidak membuat model sendiri."

"Dinding hanya dibangun di sisi wall yang bersebelahan dengan floor, jadi objeknya tidak terlalu banyak."

[Tutup F10, lihat sekeliling sebentar: dinding, lantai, corridor]

### C. Seed tampil di layar & konsisten (Tugas 2, Demo seed sama)

[Tunjuk HUD atas "PROCEDURAL DUNGEON // SEED 500" dan panel kanan atas]

"Seed yang dipakai selalu tampil di layar. Di sini seed 500 dengan algoritma BSP."

[Tekan F10, tunjuk peta dan baris "rooms … floor …" serta "Goal C_PathFarthest: BFS … cell" di panel]

"Perhatikan bentuk peta, jumlah room, luas floor, dan panjang jalur Start–Goal-nya."

[Tekan F7 — ulangi seed yang sama, buka F10 lagi]

"F7 membuat ulang dungeon dengan seed dan konfigurasi yang sama. Hasilnya identik: bentuk peta, jumlah room, luas floor, dan panjang jalur sama persis. Di eksperimen F9 kami juga membandingkan hash layout dari tiga kali generate seed 500, dan hasilnya sama. Ini karena semua angka acak berasal dari seed, dan tiap tahap punya stream random sendiri, jadi aksi player tidak menggeser hasil generation."

### D. Seed berbeda → variasi (Demo seed berbeda)

[Tekan F6, buka F10, tunjuk peta. Ulangi F6 sekali atau dua kali]

"F6 mengambil seed baru. Lihat, jumlah room, posisi room, arah corridor, posisi Start dan Goal semuanya berubah. Tapi aturannya tetap: semua room terhubung dan Goal selalu bisa dicapai."

"Selanjutnya Zhan akan menjelaskan algoritma dan parameternya."

---

## P2 — Zhan: Algoritma, parameter, validasi (±4–5 menit)

### A. BSP room–corridor dan Random Walk (Tugas 1)

[Kembali ke seed 500: hentikan Play lalu Play lagi, atau gunakan seed yang sama di profile. Tekan F10]

"Teknik utama kami BSP, Binary Space Partitioning. Garis putih di peta ini adalah leaf BSP. Area 60×40 dibagi dua secara rekursif; area yang lebih lebar dipotong vertikal, yang lebih tinggi dipotong horizontal, sampai ukurannya di bawah Max Leaf Size."

"Di setiap leaf dibuat satu room yang lebih kecil dari leaf-nya karena ada Room Margin. Sisa ruangnya untuk dinding dan corridor."

"Room-room disambung corridor bentuk L: lurus horizontal lalu vertikal. Yang disambung adalah room dari dua anak BSP yang bersebelahan, jadi seluruh room pasti terhubung. Ditambah corridor loop supaya jalurnya tidak satu garis saja."

[Tekan F12]

"Sebagai alternatif ada Random Walk. Dengan seed yang sama, hasilnya lebih organik seperti gua, tanpa room. Jalur dan area tetap terhubung dan divalidasi dengan cara yang sama."

[Tekan F12 lagi untuk kembali ke BSP]

### B. Ubah parameter generator (Tugas 2, Demo ubah parameter)

[Stop Play. Tactical Outpost → Dungeon → Select Profile. Tunjuk Inspector]

"Semua parameter ada di Dungeon Profile: seed, ukuran peta, Min dan Max Leaf Size, Room Margin, lebar corridor, jumlah loop, parameter Random Walk, jumlah zombie, dan lainnya."

[Ubah Max Leaf Size jadi lebih besar (misal 28). Play → MODE 03, seed tetap 500, tekan F10]

"Kami ubah Max Leaf Size jadi lebih besar dengan seed yang sama. Leaf jadi lebih sedikit, jadi room lebih sedikit tetapi lebih luas."

[Stop, kembalikan Max Leaf Size. Ubah Room Margin jadi 4. Play → MODE 03, F10]

"Sekarang Room Margin dinaikkan. Room menyusut di dalam leaf-nya, jarak antar-room lebih jauh, dan corridor lebih panjang."

[Buka ExperimentResults.md, tunjuk Eksperimen 2 dan 3]

"Hasil ini juga kami ukur otomatis lewat F9. Contohnya Min Leaf Size 8 menghasilkan sekitar 13 leaf, sedangkan 16 hanya sekitar 5. Room Margin 4 membuat room paling kecil dan persentase floor paling rendah."

[Stop, kembalikan parameter ke default, Play → MODE 03 seed 500]

### C. Validasi BFS, repair, generate ulang (Tugas 4, Demo hasil validasi)

[Tekan F10, tunjuk daftar [OK] HARD / soft di panel kanan]

"Sebelum dipakai, setiap dungeon divalidasi dengan BFS atau flood fill dari Start. Ini hasil validasinya di layar."

"Hard constraint wajib lolos: room minimal dua, Start dan Goal di floor, Goal terjangkau dari Start, semua room terjangkau, enemy dan item di floor valid, enemy jauh dari Start, dan semua power relay bisa dicapai tanpa melewati Goal."

"Soft constraint dicatat sebagai kualitas, misalnya jarak Start–Goal cukup jauh dan ada cabang atau loop."

[Tunjuk baris Generate-and-test: attempt 1/10, repair, VALID YES]

"Kalau ada area yang terputus, generator melakukan repair: membuat corridor dari area terputus ke area terhubung terdekat. Kalau tetap gagal, dungeon di-generate ulang dengan batas 10 percobaan. Setelah props dan objek ditempatkan, BFS dijalankan lagi dengan props dianggap penghalang; props yang memutus jalur dibuang."

[Buka ExperimentResults.md, Eksperimen 8]

"Di eksperimen repair, kami sengaja menghilangkan 30 sampai 60 persen corridor. Repair membuat dungeon kembali valid."

[Tunjuk baris NavMesh: OK start → goal]

"Terakhir, jalur Start ke Goal juga dicek ulang dengan NavMesh yang di-bake dari hasil build, jadi zombie AI juga bisa berjalan di layout baru ini."

"Sitti akan lanjut ke penempatan objek dan gameplay."

---

## P3 — Sitti: Penempatan objek, main sampai goal (±5–6 menit)

### A. Start, goal, enemy, item (Tugas 3, Demo lokasi valid)

[Panel F10 terbuka, tunjuk huruf/warna di peta: S, G, relay, item, props]

"Di peta ini S adalah Start dan G adalah Goal. Goal dipilih dengan strategi jarak BFS terjauh dari Start, dan garis amber adalah critical path di antara keduanya."

[Tutup F10, tunjuk kotak hijau di lantai Start room]

"Start room adalah zona aman, ditandai kotak hijau yang mengikuti sisi-sisi room. Di zona ini tidak ada zombie maupun props."

"Penempatan objek memakai constraint. Zombie tidak boleh di wall, di safe room, atau dekat Start. Jumlahnya bertingkat menurut jarak dari Start: 3, 5, 7, lalu 9 zombie per room, dan jenisnya makin berat di room yang jauh. Boss hanya boleh di Goal room. Item ammo dan medkit diutamakan di dead-end dan room samping."

"Props seperti container, barrel, pallet, dan barrier tidak boleh di critical path atau di mulut pintu, dan tidak boleh bersebelahan supaya room tidak terputus."

[Tekan M, tunjuk ikon di peta besar]

"Di peta besar, relay ditandai bulatan magenta, generator ikon emas, medkit tanda plus hijau, dan ammo kotak kuning. Tekan M lagi untuk kembali."

[Tekan F10, tunjuk baris Enemy / Item / Relay / Props: rejected …]

"Panel ini menunjukkan berapa kandidat ditolak dan alasannya, misalnya NotFloor, NearStart, Occupied, atau OnCriticalPath. Tanpa aturan ini, objek bisa saja muncul di dinding atau tepat di samping player."

### B. Objektif dan bantuan visual (Tugas 5)

[Tutup F10, tunjuk HUD atas "ACTIVATE POWER RELAYS 0/3" dan garis amber di lantai]

"Generator terkunci sampai tiga power relay aktif. Garis amber di lantai adalah path guide hasil BFS dari posisi player ke relay terdekat yang belum aktif, lalu ke generator. Bisa dimatikan dengan F5."

[Tekan F11 — teleport ke relay, room tersegel]

"Zombie di setiap room baru muncul saat player masuk ke room itu. Begitu masuk, pintu room tertutup: ROOM SEALED. Relay di room ini belum bisa diaktifkan sebelum semua zombie room bersih."

[Tembak beberapa zombie, lalu untuk mempercepat F8 → F4; pintu terbuka; tekan E di relay]

"Room bersih, pintu terbuka, dan relay pertama aktif. Lampunya berubah dari merah ke hijau, ikonnya di peta juga jadi hijau."

[F11 + bersihkan room + E untuk relay 2 dan 3]

### C. Player start → goal, kondisi selesai (Demo player sampai goal)

[Setelah relay 3/3: HUD "ALL RELAYS ONLINE // ENTER THE GENERATOR ROOM". Ikuti path guide ke Goal room]

"Semua relay sudah aktif. Sekarang kita ikuti path guide ke Goal room."

[Masuk Goal room → BOSS AWAKE]

"Begitu masuk Goal room, boss muncul. Generator baru bisa dinyalakan setelah boss dikalahkan."

[Kalahkan boss (atau F8 → F4), dekati generator]

"Boss kalah, kita dekati generator..."

[Layar POWER RESTORED + ringkasan]

"...dan muncul POWER RESTORED: goal tercapai, lengkap dengan seed, waktu, jumlah kill, dan relay. Tekan ENTER untuk dungeon baru."

### D. Penutup

"Jadi, dungeon kami punya struktur room–corridor yang jelas dari BSP, selalu terhubung karena divalidasi BFS dengan repair dan batas percobaan, objeknya ditempatkan di posisi valid, dan levelnya bisa diselesaikan dari Start sampai Goal. Seed yang sama selalu menghasilkan dungeon yang sama, dan seed atau parameter yang berbeda memberi variasi tanpa melanggar aturan gameplay."

"Intinya, procedural generation bukan sekadar random: random ditambah algoritma, aturan, dan validasi. Terima kasih."
