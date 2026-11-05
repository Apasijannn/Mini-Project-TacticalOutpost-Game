# Naskah Presentasi — Modul 10: Procedural Dungeon (Kelompok Dingo)

| Pembicara | Bagian | Durasi |
|---|---|---|
| P1 — Fa'iz | Pembuka, konsep grid + seed, integrasi mode | ±4 menit |
| P2 — Zhan | BSP, room, corridor, start/goal, validasi BFS + repair | ±4 menit |
| P3 — Sitti | Placement + objektif (relay, clear room, boss), eksperimen, penutup | ±4 menit |

**Persiapan demo sebelum mulai:**
- Unity terbuka, Play Mode, berhenti di layar CHOOSE YOUR OPERATION.
- `Dungeon_Profile`: `Use Random Seed` OFF, `Seed` = 500 (supaya hasilnya sama dengan slide/laporan).
- Buka `Docs/Dungeon/ExperimentResults.md` di tab terpisah.

---

## P1 — Pembuka & integrasi (±4 menit)

[Layar CHOOSE YOUR OPERATION]

"Halo semuanya, kami dari Kelompok Dingo. Di modul 10 ini kami menambahkan mode ketiga ke game Tactical Outpost, namanya Procedural Dungeon."

"Dua mode sebelumnya, Mini Project dan PCG, tetap seperti dulu. Mode Dungeon dipasang terpisah lewat hook scene, jadi script game aslinya tidak kami ubah."

[Klik kartu MODE 03]

"Tujuannya beda dari dua mode lain. Di sini player tidak bertahan dari wave, tapi harus menembus kompleks yang digenerate, dari Start sampai generator di Goal."

[Tunjuk HUD atas]

"Seed-nya selalu tampil di layar. Ini penting, karena seed yang sama selalu menghasilkan dungeon yang sama."

[Tekan F10, tunjuk peta grid]

"Semua dimulai dari grid. Dungeon kami 60 kali 40 cell, sesuai default modul. Tiap cell punya tipe: Wall, Floor, Start, atau Goal. Satu cell kami petakan jadi 3,5 meter di dunia Unity."

"Grid ini kemudian dibangun jadi scene pakai prefab yang sudah ada di project: lantai jalan beton, dinding panel beton, kolom di sudut, lampu jalan, burning barrel, dan generator. Tidak ada objek yang kami buat sendiri."

"Biar ringan, dinding hanya dibangun di sisi yang menghadap floor. Kalau ditanya modul soal `IsWallVisible`, itu versi kami."

"Selanjutnya Zhan akan jelaskan algoritmanya."

---

## P2 — BSP, start/goal, validasi (±4 menit)

[Tetap di panel F10, tunjuk garis putih di peta]

"Terima kasih. Garis putih ini hasil BSP, Binary Space Partitioning. Area 60×40 dibagi dua secara rekursif. Kalau terlalu lebar dipotong vertical, kalau terlalu tinggi dipotong horizontal, sampai ukurannya di bawah maxLeafSize 20. Node terakhir disebut leaf."

"Di setiap leaf kami buat room, tapi tidak memenuhi leaf karena ada roomMargin 2 cell. Sisa ruang itu dipakai untuk dinding dan corridor."

"Room lalu disambung L-corridor. Kami menghubungkan room terdekat dari subtree kiri dan kanan BSP, jadi semua room pasti tersambung. Ditambah satu corridor loop supaya dungeon tidak terlalu linear."

[Tunjuk garis amber]

"Start ada di room pertama. Untuk Goal kami pakai strategi C dari modul: room dengan jarak BFS terjauh dari Start. Garis amber ini critical path-nya."

[Tunjuk daftar [OK] di panel]

"Sebelum dipakai, dungeon divalidasi dulu dengan BFS. Hard constraint-nya: room minimal dua, Start dan Goal di floor, Goal terjangkau, semua room terjangkau, dan enemy minimal 8 cell dari Start."

"Kalau gagal, generator melakukan repair, yaitu carve corridor dari area terputus ke area terdekat. Kalau masih gagal, generate ulang dengan batas 10 attempt."

[Tekan F7]

"F7 mengulang seed yang sama. Layout-nya identik."

[Tekan F12]

"F12 mengganti algoritma ke Random Walk dengan seed yang sama. Hasilnya lebih organik seperti gua, tidak ada room. Validasinya tetap sama."

[Tekan F12 lagi untuk kembali ke BSP]

"Sitti akan lanjut ke isi dungeon dan hasil eksperimen."

---

## P3 — Placement, eksperimen, pengembangan (±4 menit)

[Panel F10, tunjuk titik merah/cyan/coklat]

"Isi dungeon juga procedural, tapi dengan constraint. Titik merah zombie, ungu boss, cyan item, coklat props cover."

"Zombie tidak boleh di wall, di safe room, atau dekat Start. Boss hanya boleh di Goal room. Item diutamakan di dead-end dan room yang bukan Start atau Goal, jadi eksplorasi ada hadiahnya."

"Props tidak boleh berada di critical path atau di mulut pintu. Setelah semua ditempatkan, BFS dijalankan lagi dengan props dianggap penghalang. Kalau ada yang memutus jalur, props itu dibuang."

[Tutup F10, tunjuk HUD "ACTIVATE POWER RELAYS 0/3"]

"Supaya tiap bagian dungeon punya tujuan, ada tiga objektif. Pertama, generator terkunci. Kita harus menyalakan tiga power relay dulu. Relay ini juga ditempatkan secara procedural: saling berjauhan, minimal satu di room yang tidak dilewati jalur utama, dan masing-masing dijaga zombie. BFS memastikan semua relay bisa dicapai tanpa melewati Goal."

"Path guide di lantai selalu menunjuk relay terdekat yang belum aktif."

[Tekan F11 → room tersegel]

"Kedua, clear room. Begitu kita masuk room yang ada zombienya, pintunya naik dan room tersegel. Relay di sini menolak diaktifkan sebelum room bersih."

[Habisi zombie (atau F8 → F4), pintu turun, tekan E]

"Room bersih, pintu terbuka, relay pertama online."

[F11 + E untuk relay 2 dan 3]

"Ketiga, boss. Begitu relay terakhir aktif, boss muncul di Goal room dan langsung memburu kita. Generator baru bisa dinyalakan setelah boss mati."

[Kalahkan boss, jalan ke generator]

"Dan sekarang generator bisa dinyalakan: POWER RESTORED."

[Buka ExperimentResults.md]

"Ini hasil eksperimen F9. Lima seed, semuanya valid di attempt pertama, dengan jumlah room 7 sampai 12."

"Semakin besar minLeafSize, leaf makin sedikit tapi room makin luas: dari 13 leaf jadi sekitar 5."

"Untuk strategi Goal, rata-rata jarak BFS strategi A 67 cell, B 70, dan C 88. Jadi strategi C memberi jalur terpanjang."

"Di eksperimen repair, kami sengaja menghilangkan 30 sampai 60 persen corridor. Repair BFS tetap membuat semua dungeon valid."

"Pengembangan yang sudah kami terapkan: validasi BFS, NavMesh runtime, boss room, treasure di dead-end, enemy density, corridor width, runtime regeneration, dan dungeon metrics."

"Untuk relay, 1 relay membuat rute 1,2 kali lebih panjang, 3 relay 1,5 kali, dan 5 relay sekitar 2,2 kali jalur langsung, dan semuanya tetap valid."

"Kesimpulannya, PCG bukan sekadar random. PCG adalah random ditambah algoritma, rule, dan validasi. Terima kasih."
