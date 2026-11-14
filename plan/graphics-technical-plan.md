# SkyGate: Plan kỹ thuật đồ hoạ A320 VietJet (Unity 6 URP)

> Người đọc chính: **Sonnet 5.5**, người sẽ viết code theo tài liệu này. Người dùng duyệt plan và thao tác trong Unity Editor.
> Nguồn ý tưởng: `plan/idea.md` (artifact "Kế hoạch SkyGate"). Tài liệu này chỉ bàn phần **đồ hoạ và rendering**: model 3D, vật liệu, livery, buồng lái, khoang khách, ánh sáng, camera, chất lượng và hiệu năng. Mô hình bay, ATC và tutorial nằm ở plan khác. Ở đây chỉ định nghĩa **giao diện dữ liệu** mà các phần đó phải ghi vào.
> Thiết bị ưu tiên tối ưu: **Redmi Turbo 4 Pro**.

---

## 0. Cách Sonnet dùng tài liệu này

1. Làm theo thứ tự task ở **mục 14** (G0 → G14). Mỗi task có file cần tạo và tiêu chí "xong khi".
2. Không tự đổi các quy ước ở mục 2–5 (toạ độ, tên node, layer, luồng dữ liệu). Nếu thấy cần đổi, ghi vào mục "Câu hỏi mở" cuối file rồi hỏi người dùng.
3. Mỗi task phải chạy được trong Editor (Play mode) trước khi sang task sau. Người dùng sẽ gửi log/ảnh chụp nếu lỗi.
4. Không có Unity trong môi trường của Claude, nên code không compile được tại chỗ. Khi viết, ưu tiên API ổn định và ghi rõ chỗ nào phụ thuộc phiên bản URP (xem mục 15).
5. Thư mục `SkyGate/` đã có vài file khởi đầu từ phiên trước: `Packages/manifest.json`, `Scripts/Core/*` (AircraftVisualState, A320Data, LiveryDefinition, AircraftStateBus), `Scripts/Rendering/SkyLayers.cs`, `Scripts/Rendering/Procedural/MeshBuilder.cs`. Được dùng lại hoặc ghi đè cho khớp với plan này.

---

## 1. Thiết bị mục tiêu và ngân sách

### 1.1 Redmi Turbo 4 Pro (máy thử chính)

| Thông số | Giá trị (cần xác nhận bằng log `SystemInfo` lần chạy đầu) |
|---|---|
| SoC / GPU | Snapdragon 8s Gen 4, Adreno 825 (GPU tile-based) |
| RAM | 12–16 GB LPDDR5X |
| Màn hình | 6,83", 2772×1280, OLED 120 Hz |
| OS | Android 15 (HyperOS 2) |
| API đồ hoạ | Vulkan (chính), OpenGL ES 3.2 (dự phòng) |

Hệ quả cho thiết kế:

- **GPU tile-based**: MSAA 4x gần như miễn phí, nên dùng MSAA thay cho FXAA/SMAA. Tránh mọi thứ làm "resolve" tile giữa frame: tắt Depth Texture, Opaque Texture, tránh full-screen pass không cần thiết, bật *Store Actions Optimization*.
- **Độ phân giải gốc rất cao** (3,5 MP). Render ở 100% là phí. Mặc định render scale **0,8** (≈2218×1024) và có **render scale động** trong khoảng 0,7–0,9 theo thời gian frame.
- **120 Hz**: game nhắm **60 fps** ổn định ở mức High. Không chạy 120 fps vì máy sẽ nóng và tụt xung. Phải đặt `Application.targetFrameRate = 60` và `vSyncCount = 0`.
- **Nhiệt**: chuyến bay dài 20–60 phút nên ưu tiên hiệu năng *bền vững*, không chỉ đỉnh. Có bộ hạ cấp theo nhiệt (mục 11.4).

### 1.2 Ngân sách cho Turbo 4 Pro ở mức High (60 fps = 16,6 ms)

| Hạng mục | Ngân sách |
|---|---|
| GPU frame time | ≤ 11 ms (chừa 30% cho nhiệt) |
| CPU main thread | ≤ 8 ms |
| Draw call (SetPass) cảnh ngoài, ở sân bay | ≤ 250 (≤ 120 SetPass) |
| Draw call trong buồng lái | ≤ 180 |
| Tam giác hiển thị | ≤ 1,2 triệu |
| Máy bay ngoài LOD0 | 60–90k tam giác (idea.md: 50–80k, máy này dư sức nhận mức trên) |
| Buồng lái | 80–120k tam giác |
| Khoang khách | ≤ 60k tam giác (ghế gộp mesh) |
| Texture máy bay | 2K (thân 4096×1024 được phép cho riêng livery thân) |
| Bộ nhớ texture tổng | ≤ 600 MB |
| Dung lượng APK/AAB | < 1 GB (idea.md) |

Các mức Medium/Low vẫn giữ để chạy trên máy tầm trung (idea.md mục 09), nhưng **chỉ tinh chỉnh High trên Turbo 4 Pro trước**. Medium/Low chỉ cần chạy đúng, chưa cần đo.

---

## 2. Cấu trúc dự án

```
skygate-flight/
  plan/                         # idea.md, plan này
  SkyGate/                      # Unity project root (mở bằng Unity Hub > Add project from disk)
    Packages/manifest.json
    Assets/SkyGate/
      Scripts/
        Core/        SkyGate.Core.asmdef       # dữ liệu thuần, không phụ thuộc URP
        Rendering/   SkyGate.Rendering.asmdef  # mọi thứ hiển thị lúc chạy
          Procedural/  Aircraft/  Cockpit/  Cabin/  Avionics/  Cameras/
          Environment/ Quality/  Input/  Demo/
        Editor/      SkyGate.Editor.asmdef     # generator, setup, import rules
      Art/           # model/texture thật (tải hoặc tự làm), có CREDITS.md
        Aircraft/A320/  Vehicles/  Airport/  People/
      Generated/     # do generator tạo, KHÔNG sửa tay: Meshes/ Textures/ Materials/ Prefabs/
      Data/          # ScriptableObject: livery, quality profile, cabin layout
      Settings/      # URP assets, renderer, volume profile
      Scenes/        # GraphicsSandbox.unity (scene thử đồ hoạ)
```

### 2.1 Assembly

| asmdef | Tham chiếu | Ghi chú |
|---|---|---|
| `SkyGate.Core` | không | struct trạng thái, bảng số A320, LiveryDefinition |
| `SkyGate.Rendering` | Core, `Unity.RenderPipelines.Core.Runtime`, `Unity.RenderPipelines.Universal.Runtime`, `Unity.InputSystem`, `UnityEngine.UI` | `versionDefines`: `com.unity.inputsystem` → `SKYGATE_INPUTSYSTEM` |
| `SkyGate.Editor` | Core, Rendering, URP Runtime, `UnityEngine.UI`, `Unity.InputSystem` | `includePlatforms: ["Editor"]` |

Code dùng Input System bọc trong `#if ENABLE_INPUT_SYSTEM && SKYGATE_INPUTSYSTEM`, có nhánh `UnityEngine.Input` cũ dự phòng.

### 2.2 Layer (tạo bằng menu setup, mục G0)

| Chỉ số | Tên | Dùng cho |
|---|---|---|
| 8 | `Avionics` | canvas màn hình buồng lái, chỉ camera atlas thấy |
| 9 | `Interior` | buồng lái và khoang khách. Chỉ vẽ khi camera ở trong máy bay |
| 10 | `ExteriorHull` | vỏ thân. Không vẽ khi camera ở trong, để nắng lọt qua lỗ cửa sổ của vỏ trong |

Code luôn lấy layer qua `SkyLayers` (`LayerMask.NameToLayer`, có giá trị dự phòng), không hard-code số.

---

## 3. Luồng dữ liệu: mô phỏng → hình ảnh

```
FlightModel / AircraftSystems / GroundOps / ATC   (plan khác)
          │  ghi mỗi frame
          ▼
AircraftStateBus  (MonoBehaviour ở root prefab máy bay)
  └─ AircraftVisualState state   (struct, xem SkyGate/Assets/SkyGate/Scripts/Core/AircraftVisualState.cs)
  └─ chuỗi FMA, LiveryDefinition
          │  chỉ đọc
          ▼
AircraftVisualRig · AircraftLights · AvionicsSystem · CockpitControls · CameraDirector · InteriorAmbient
```

Quy tắc bắt buộc:

- Rendering **không bao giờ** ghi vào mô phỏng. Tương tác buồng lái (bấm nút) phát `event` qua `CockpitControl.Pressed(id)`. Hệ thống đăng ký nghe rồi tự đổi state.
- `AircraftVisualState` là struct, chỉ chứa số, enum và bool. Không chứa reference. Dữ liệu dạng chuỗi (FMA) để trên bus.
- Giá trị trong state là **giá trị đã nội suy theo thời gian** (ví dụ `gearPos` chạy dần 0→1). Rig không tự làm hoạt ảnh, chỉ ánh xạ số sang góc. Nhờ vậy replay và đồng bộ đơn giản.
- Khi chưa có mô phỏng, `DemoStateDriver` ghi state giả: lắc bề mặt điều khiển, bật/tắt càng, flap, đèn, cửa, bay vòng tròn.

---

## 4. Hệ toạ độ và số liệu A320

**Toạ độ máy bay (Unity):** 1 đơn vị = 1 m. +Z = mũi, +Y = lên, +X = cánh phải. Gốc: y = 0 là mặt đất khi càng thả, x = 0 là trục giữa, z = 0 gần trọng tâm (~25% MAC).
**s** = khoảng cách dọc thân tính từ chóp mũi, `z = NoseZ − s`. **yRel** = độ cao so với tâm thân.

| Đại lượng | Giá trị | Đại lượng | Giá trị |
|---|---|---|---|
| Dài | 37,57 m | NoseZ | +17,0 |
| Sải cánh (có sharklet) | 35,80 m | Nửa sải (không sharklet) | 17,05 m |
| Cao | 11,76 m | Tâm thân y | 3,95 m |
| Bán rộng / bán cao thân | 1,975 / 2,07 m | Sàn khoang | yRel −0,70 |
| Diện tích cánh | 122,6 m² | Mũi tên ¼ dây cung | 25° |
| Góc nhị diện | 5,1° | Động cơ x | ±5,75 m |
| Khoảng cách 2 càng chính | 7,59 m | Càng chính z | −1,5 |
| Chiều dài cơ sở | 12,64 m | Càng mũi z | +11,14 |
| Bánh chính r | 0,585 m | Bánh mũi r | 0,38 m |

**Cánh (phải):** dây cung c(0) = 7,2; c(6,3 kink) = 4,35; c(17,05) = 1,45. Đường ¼ dây cung: `zqc(x) = 3,03 − x·tan25°`. LE: `zqc + 0,25c`. Độ cao LE: `y = 2,55 + x·tan5,1°`. Góc đặt cánh: +2° ở gốc, −1° ở mút. Độ dày: 15% / 12% / 10,5%. Profile NACA 4 chữ số kiểu 2412 (m = 0,02, p = 0,4).

**Biên dạng thân** (dùng chung cho mesh, livery và nội thất, đặt trong `FuselageProfile`):

- Mũi s ∈ [0; 6,2]: hàm siêu elip `f(t) = (1 − (1−t)^p)^(1/p)`. Bề rộng p = 2,2, đỉnh p = 2,0, đáy p = 2,6. Chóp mũi thấp hơn tâm 0,55 m.
- Giữa: tròn, bán kính 1,975 / 2,07.
- Đuôi s ∈ [25,07; 37,57]: đáy vểnh lên (`t^1,25`), đỉnh hạ chậm (`t^2`), cuối ống xả APU r = 0,28 ở yRel +1,05.
- Bụng (belly fairing) s ∈ [12; 23,5]: đáy thấp thêm 0,30 m, nửa dưới phình rộng 8%.
- Tham số bề mặt `Surface(s, θ)`: θ = 0 ở đáy, tăng về phía +X, π ở đỉnh. UV thân: `u = s/L`, `v = θ/2π`.

**Đuôi:** ổn định ngang nửa sải 6,225 m, dây gốc/mút 3,9/1,35, LE mũi tên 32°, nhị diện 6°, đặt ở s ≈ 31,5, y ≈ 4,47. Đuôi đứng cao 5,9 m, dây gốc/mút 5,9/2,1, LE mũi tên 34°, gốc LE ở s ≈ 26,5, y = 5,6.

**Bảng flap** (`A320Data.FlapConfigs`): 0 → 0/0; 1+F → 18/10; 2 → 22/15; 3 → 22/20; FULL → 27/40 (slat°/flap°).
**Giới hạn lệch:** aileron ±25°, elevator +30/−17°, rudder ±30°, spoiler 50°, lái bánh mũi ±75°. N1 100% ≈ 5000 rpm.

Đây là số liệu công khai dùng cho game, không dùng cho bay thật.

---

## 5. Chiến lược model 3D

### 5.1 Hai giai đoạn, một "hợp đồng"

1. **Placeholder thủ tục (G2–G5):** generator C# trong Editor dựng toàn bộ A320 (ngoài, buồng lái, khoang) từ số liệu mục 4, bake livery, lưu mesh/texture/material/prefab vào `Generated/`. Có ngay một máy bay đúng kích thước, đúng pivot, chuyển động được.
2. **Model thật (M6):** model từ Blender, Sketchfab hoặc Asset Store thay vào **mà không sửa code**, miễn tuân thủ hợp đồng 5.2. `ArtImportRules` (AssetPostprocessor) kiểm tra hợp đồng khi import và in cảnh báo.

### 5.2 Hợp đồng model (bắt buộc cho cả placeholder lẫn model thật)

**Chung:** đơn vị mét, mũi hướng +Z, lên +Y, root rotation = identity, scale = 1. Từ Blender: xuất FBX với *Apply Transform*. Unity bật *Bake Axis Conversion*. Thử bằng một khối có mũi tên trước khi xuất model thật.

**Tên node và pivot** (hằng số trong `AircraftPartNames`):

| Node | Pivot / trục | Kênh hoạt ảnh |
|---|---|---|
| `Fuselage` (layer ExteriorHull) | gốc máy bay | — |
| `Wing_L`, `Wing_R` | gốc | — |
| `Slat_{L,R}_{1..5}` | bản lề giả ở 16% dây cung, dưới mặt dưới 6% c. Trục X dọc bản lề hướng +X | `SlatDeg`, + = chúc xuống |
| `FlapIn_{L,R}`, `FlapOut_{L,R}` | bản lề ở 74% dây cung. Trục X hướng +X | `FlapDeg` (−θ = mép sau xuống) + `FlapTravel` (lùi theo −Z cục bộ, tối đa ~0,9 × dây flap) |
| `Aileron_{L,R}` | 74% dây cung | `AileronL = −aileron·25`, `AileronR = +aileron·25` (+θ = mép sau lên) |
| `Spoiler_{L,R}_{1..5}` | 60% dây cung, trên mặt trên | 0..50°, + = dựng lên |
| `HStab_{L,R}`, `Elevator_{L,R}` | bản lề 70% | `+elevator·30` / `·17` khi âm |
| `VStab`, `Rudder` | bản lề 68%. Trục X hướng lên, Y hướng +X | `Rudder = −rudder·30` (+θ = mép sau sang trái) |
| `Engine_1` (trái), `Engine_2` (phải) | tâm cửa hút, trục Z | — |
| `Fan_{1,2}` | trục Z động cơ | quay theo N1 (giới hạn tốc độ hiển thị) |
| `Reverser_{1,2}` | — | trượt −Z 0,45 m |
| `MainGear_{L,R}` | điểm gắn trên cùng, trục máy bay | thu vào trong quanh Z: R = −θ, L = +θ, 90° |
| `MainGear_{L,R}_Wheels` | tâm trục bánh | quay quanh X |
| `NoseGear` | điểm gắn | thu về trước quanh X: −95° |
| `NoseGear_Steer` → `NoseGear_Wheels` | trục đứng / trục bánh | lái quanh Y (+ = phải), bánh quay quanh X |
| `Door_{L1,R1,L4,R4}`, `Door_Cargo{Fwd,Aft}` | mép bản lề phía trước | mở ngoài quanh Y. Ẩn renderer khi = 0 (placeholder không khoét lỗ vỏ) |
| `Light_*` | vị trí đèn | xem 6.5 |
| `Interior` (layer Interior) → `Cockpit`, `Cabin` | gốc | — |
| `Anchors` → `Eye_CPT`, `Eye_FO`, `Cam_Cabin_*`, `Attach_*` | điểm gắn camera và dịch vụ mặt đất | — |

**Anchor cho GroundOps (M2):** `Attach_Towbar` (trước bánh mũi), `Attach_JetBridge_L1`, `Attach_Stairs_L1`, `Attach_Stairs_L4`, `Attach_GPU` (bên phải mũi, thấp), `Attach_Fuel_R` (dưới cánh phải x ≈ 8), `Attach_CargoFwd`, `Attach_CargoAft` (bên phải, thấp), `Attach_Catering_R1`, `Attach_Catering_R4`.

**LOD:** mesh con đặt tên `<Node>_LOD0/1/2` (trùng quy ước Unity). Một `LODGroup` ở root gom mọi renderer ngoại thất theo chỉ số LOD. Nội thất không vào LODGroup. LOD2 bỏ spoiler, cánh quạt, chi tiết càng.

| LOD | Chuyển khi màn hình chiếm | Ngân sách tam giác |
|---|---|---|
| 0 | > 25% | 60–90k |
| 1 | 8–25% | ~25k |
| 2 | 0,4–8% | ~6k |
| cull | < 0,4% | — |

**UV:** UV0 cho texture (thân dùng UV trụ `u = s/L`, `v = θ/2π` để livery bake được). UV1 dành cho AO/lightmap của nội thất (model thật bake trong Blender). Model thật được dùng UV livery riêng. Khi đó livery là texture vẽ tay, còn `LiveryDefinition` chỉ dùng cho màu của các phần không có texture.

**Vật liệu theo slot** (tên material quyết định ánh xạ khi import): `A320_Fuselage`, `A320_Tail`, `A320_Wing`, `A320_Nacelle`, `A320_Metal`, `A320_EngineDark`, `A320_Gear`, `A320_Tire`, `A320_LightLens`, `Cockpit_Panel`, `Cockpit_Display`, `Cockpit_Shell`, `Cabin_Wall`, `Cabin_Seat`, `Cabin_Headrest`, `Cabin_Frame`, `Cabin_Carpet`, `Cabin_Bin`, `Cabin_Light`.

### 5.3 MeshBuilder (đã có trong `Scripts/Rendering/Procedural/MeshBuilder.cs`)

- Mọi đỉnh có normal do người gọi cung cấp. `AddTriangle` tự đảo chiều quấn cho khớp normal (Unity: mặt trước = `Cross(b−a, c−a)`). Mesh nội thất chỉ cần normal hướng vào trong.
- **Normal tính giải tích**, không gọi `RecalculateNormals`:
  - airfoil: xoay tiếp tuyến 2D theo chiều đi **cùng chiều kim đồng hồ** (mặt trên đi tới, mặt sau đi xuống, mặt dưới đi lui, mặt trước đi lên), lấy `n = (−t.y, t.x)`;
  - thân: sai phân hữu hạn của `Surface(s, θ)`, ép hướng ra ngoài trục;
  - tiện xoay (lathe): `n = (t.r, −t.z)` theo chiều đi profile.
- Cánh cắt theo dây cung: slat [0; 0,13], thân cánh [0,13; 0,74], mép sau [0,74; 1]. Mỗi mảnh gồm các polyline (mặt trên, mặt dưới, mặt bịt phẳng) loft theo các trạm sải cánh. Ranh giới mảnh phải là trạm loft. Mảnh có a = 0 hoặc b = 1 nối mặt trên và dưới thành một polyline để mép trước mượt.
- Bề mặt điều khiển: dựng trong toạ độ máy bay → `Localize(pivot, FrameFromAxes(trục bản lề, up))`. Cánh trái = `MirrorX()` của cánh phải. Trục bản lề trái lấy ngược dấu để vẫn hướng +X, nên quy ước dấu góc giống hệt hai bên.

---

## 6. Ngoại thất

### 6.1 Mesh placeholder (`A320ExteriorGenerator`, Editor)

| Phần | LOD0 | Ghi chú |
|---|---|---|
| Thân | ~180 trạm × 72 đoạn | trạm dày ở mũi (`t^1,7`) và đuôi. Nắp đuôi = ống xả APU màu tối |
| Cánh + slat + mép sau | 40 điểm dây cung | slat: [1,9–4,6], [6,6–9,2], [9,2–11,8], [11,8–14,4], [14,4–16,9]. Khe trống đắp mép trước cố định |
| Flap / aileron | — | flap trong [1,6–6,3], flap ngoài [6,3–12,4], aileron [12,6–16,2]. Còn lại là mép sau cố định |
| Spoiler | tấm 2 cm | [2–4], [4–6,2], [6,6–8,6], [8,6–10,5], [10,5–12,3], dây cung 0,60–0,745, nhô 1,2 cm trên mặt trên |
| Sharklet | cao 2,43 m | nghiêng ngoài 15°, LE mũi tên 35°, dây 1,45 → 0,55, NACA 8% đối xứng |
| Đuôi đứng, đuôi ngang | profile đối xứng 10–11% | — |
| Nacelle | lathe 48 đoạn | inlet z = 3,8, tâm y = 1,65. Profile (z, r): cửa hút (−0,95; 0,78) → mép (0; 0,86) → ngoài (−0,18; 0,975) → (−2,35; 0,99) → ống bọc reverser đến (−3,55; 0,80) → lõi (−3,3; 0,66) → (−4,5; 0,45) → nón xả (−5,25; 0,03). Mép cửa hút là kim loại, ống cửa hút tối |
| Quạt | côn 0,27 m + 22 cánh mỏng xoắn 35° | đĩa tối phía sau |
| Pylon | khối 8 góc | từ nóc nacelle lên mặt dưới cánh, z 2,6 → −2,9 |
| Càng chính | ống + 2 lốp | mỗi bên 2 lốp cách ±0,46 m, có thanh chống và tấm cửa gắn chân càng |
| Càng mũi | 2 lốp ±0,2 m | đèn taxi gắn trên ống càng |

### 6.2 Vật liệu ngoại thất (URP/Lit)

| Material | Base | Smooth | Metal | Ghi chú |
|---|---|---|---|---|
| A320_Fuselage | livery thân | 0,62 | 0,05 | clearcoat giả bằng smoothness |
| A320_Tail | livery đuôi | 0,62 | 0,05 | dùng cho VStab và Rudder |
| A320_Wing | `wingColor` | 0,45 | 0,25 | — |
| A320_Nacelle | `nacelleColor` | 0,60 | 0,05 | — |
| A320_Metal | xám sáng | 0,75 | 0,9 | mép inlet, ống xả |
| A320_EngineDark | xám tối | 0,3 | 0,4 | — |
| A320_Gear / A320_Tire | xám nhạt / đen cao su | 0,4 / 0,15 | 0,3 / 0 | — |
| A320_LightLens_{Red,Green,White} | — | — | — | emission bật, chỉnh độ sáng bằng MaterialPropertyBlock |

Quy tắc: dùng chung material tối đa để SRP Batcher gom. Chỉ đèn được dùng MaterialPropertyBlock (số lượng ít). Không dùng normal map ở placeholder. Model thật được dùng normal + mask map (ASTC 4x4 / 6x6).

### 6.3 Livery (`LiveryDefinition` → `LiveryBaker`)

`LiveryDefinition` (ScriptableObject, đã có trong Core) chứa màu, vị trí chữ, số đăng ký, texture logo do người dùng cung cấp. Đổi hãng = tạo asset mới.

**Bake thân** (texture 4096×1024, sRGB, clamp): với mỗi texel (u, v) tính điểm 3D `P = Surface(u·L, v·2π)`, rồi đánh giá các lớp theo thứ tự, mỗi lớp có cạnh mềm (AA ≈ kích thước 1 texel):

1. Nền `fuselageBase`. Dưới `bellySplitYRel` dùng `bellyColor`. s < 1,0 dùng `radomeColor`.
2. Dải trang trí phía đuôi (từ `swooshStartS`): dải cong theo yRel tăng dần về đuôi, có viền `swooshAccent`.
3. Chữ hãng: dùng texture `fuselageWordmark` nếu có, không thì vẽ `titleText` bằng **font khối 5×7** (lấy mẫu 2×2). Hai bên đều đọc đúng chiều: bên trái `u_img = (s − (sc − w/2))/w`, bên phải `u_img = ((sc + w/2) − s)/w`. Chỉ vẽ khi `|P.x| > 0,35·hw`.
4. Số đăng ký (`registration`, mặc định giả định VN-A699) ở phần thân sau, hai bên.
5. Viền cửa (dày 1,8 cm, góc bo) theo bảng cửa trong `A320Windows`.
6. Cửa sổ khách: hình chữ nhật siêu elip 0,23 × 0,33 m, tâm yRel +0,30, bước 0,533 m, s từ 7,25 đến 30,2, bỏ qua vùng cửa L1/L4.
7. Kính buồng lái: 3 ô mỗi bên, đa giác trong toạ độ chiếu cạnh (s, yRel): ô 1 s ∈ [1,0; 1,9] (có trụ giữa |x| < 0,07), ô 2 [1,98; 2,55], ô 3 [2,62; 3,05]. Màu kính có gradient phản chiếu trời.

**Bảng cửa** (`A320Windows.Doors`, toạ độ s, yRel):

| Cửa | s tâm | Rộng × cao | Đáy (yRel) | Bên |
|---|---|---|---|---|
| L1 / R1 | 6,1 | 0,81 × 1,85 | −0,70 | trái / phải |
| Thoát hiểm trên cánh (×2) | 15,9 và 16,75 | 0,51 × 1,02 | −0,08 | cả hai |
| L4 / R4 | 31,0 | 0,81 × 1,85 | −0,70 | trái / phải |
| Hàng trước / sau | 8,6 / 24,5 | 1,82 × 1,24 | −1,75 | phải |

**Bake đuôi đứng** (2048×2048): nửa trái texture = mặt trái, nửa phải = mặt phải, cả hai đọc đúng chiều. Có nền `tailBase`, dải chéo `tailAccent`, `tailLogo` của người dùng.

**Vỏ trong (nội thất)** dùng **chính các luật cửa sổ trên** để tạo alpha (lỗ) cho texture vỏ trong. Nhờ vậy lỗ nhìn từ trong luôn trùng ô sơn bên ngoài.

**Pháp lý:** màu mặc định chỉ là ước lượng kiểu VietJet (đỏ ~#EE1C25, vàng ~#FFD200, thân trắng). Người dùng đối chiếu ảnh thật rồi chỉnh trong Inspector. Logo và chữ VietJet là thương hiệu: người dùng tự cung cấp texture, chỉ dùng phi thương mại, đặt trong `Art/Private/` (đã gitignore), ghi nguồn vào `CREDITS.md`.

### 6.4 Rig hoạt ảnh (`AircraftVisualRig`)

- Dữ liệu điều khiển là danh sách `PartBinding { node, rotChannel, rotAxis, rotScale, rotScaleNeg, moveChannel, moveAxis, moveScale, spin, maxSpinDegPerSec, hideWhenZero }`. Mặc định A320 sinh từ `A320RigDefaults` khi `Reset()`. Model thật chỉnh trục/dấu trong Inspector, không sửa code.
- `Awake`: dựng dictionary tên → Transform một lần, cảnh báo **một lần** cho node thiếu. `LateUpdate`: tính mảng kênh `float[]` từ state (không cấp phát), đặt mọi transform về tư thế nghỉ, rồi nhân các delta. Nhiều binding trên một node thì cộng dồn, ví dụ sidestick có cả pitch lẫn roll.
- Quy ước dấu (đã kiểm tra toán, ghi rõ trong comment code): mép sau lên = +θ quanh trục bản lề hướng +X. Slat chúc = +θ. Rudder: +θ = mép sau sang trái. Càng chính phải thu = −θ quanh +Z. Càng mũi thu = −θ quanh +X. Lái bánh mũi sang phải = +θ quanh +Y. Bánh lăn tới = +θ quanh +X. Cửa trái mở ra = +θ quanh +Y, cửa phải = −θ.
- Spoiler: `max(speedbrake·0,8, groundSpoilers, rollAssist)`. Roll assist là spoiler 2–5 bên cánh hạ.
- Quạt: `deg/s = N1% / 100 · 5000 · 6`, giới hạn hiển thị 1500°/s. Mục M6: thêm đĩa mờ (blur disc) khi N1 > 30%.

### 6.5 Đèn (`AircraftLights`)

| Đèn | Vị trí | Màu | Nhịp |
|---|---|---|---|
| Nav L / R | mút cánh trái / phải | đỏ / xanh lá | sáng liên tục |
| Nav tail | đuôi (s = L) | trắng | liên tục |
| Beacon trên / dưới | nóc thân s ≈ 17 / bụng | đỏ | chớp 1 Hz, 0,1 s, hai đèn lệch 0,5 s |
| Strobe | mút cánh + đuôi | trắng | chớp đôi mỗi 1 s (0 và 0,15 s, mỗi lần 0,05 s) |
| Landing L / R | dưới cánh gần thân (±3,0; 2,2; 3,0) | trắng ấm | Spot Light thật ở High, range 600, góc 12° |
| Taxi / T.O. | ống càng mũi | trắng | Spot range 150, góc 30°. Tắt khi càng thu |
| Runway turnoff | càng mũi, chếch ±30° | trắng | — |
| Logo | mặt trên đuôi ngang, rọi lên đuôi đứng | trắng | — |
| Wing | thân bên, trước cánh | trắng | — |

- Mỗi đèn gồm: thấu kính emissive (MaterialPropertyBlock `_EmissionColor`), **billboard hào quang** (quad additive dùng `URP/Particles/Unlit`, luôn quay về camera, kích thước tối thiểu theo góc nhìn để còn thấy từ xa), và Light thật tuỳ chọn.
- Độ sáng hào quang nhân `SunController.NightFactor` (ban ngày tối thiểu 0,25 để strobe vẫn thấy).
- Light thật **chỉ** cho landing, taxi, turnoff. Không đổ bóng. Bật theo `TierSettings.realtimeAircraftLights`. Ngân sách: tối đa 4 additional light trên mỗi object.

---

## 7. Buồng lái

### 7.1 Bố cục (toạ độ s, x, yRel; cho phép lệch ±5 cm)

| Thành phần | Vị trí / kích thước |
|---|---|
| Mắt phi công | CPT (−0,53; s 2,75; yRel +0,45), FO (+0,53; …). `Eye_CPT`, `Eye_FO` |
| Vỏ trong | loft s ∈ [0,75; 4,3], lùi vào 8 cm, normal hướng vào. Nắp trước (vách radome), vách sau (cửa buồng lái) |
| Sàn | yRel −0,70, bề rộng theo biên dạng ở từng s (không lòi ra ngoài vỏ) |
| Bảng đồng hồ chính | mặt ở s ≈ 2,0, x ±0,95, yRel −0,30 → +0,22, ngả sau 12° |
| 6 màn hình (DU) | 0,19 m vùng sáng, viền 0,22. CPT PFD x −0,70, CPT ND −0,46, E/WD 0 (yRel +0,05), SD 0 (yRel −0,20), FO ND +0,46, FO PFD +0,70 |
| Glareshield + FCU | s 1,85–2,15, yRel 0,22–0,30. FCU giữa x ±0,45 với 4 núm SPD/HDG/ALT/VS. EFIS ở x ±0,7 |
| Bàn điều khiển giữa (pedestal) | rộng 0,50, s 2,05–3,35, mặt yRel ≈ 0. 2 MCDU phía trước (x ±0,13), cần ga x ±0,06 ở s 2,75, cần flap x +0,18, speedbrake x −0,18 |
| Bàn bên + sidestick | x ±1,0 → ±1,45, yRel −0,05. Sidestick ở x ±1,18, s 2,35 |
| Overhead | s 1,95 → 3,3, yRel 1,05 → 1,25, x ±0,48. Lưới 8×10 nút hộp nhỏ |
| Ghế | x ±0,53, đệm ở s 3,05, đệm ngồi yRel −0,22, tựa lưng cao tới +0,65 |
| Cần càng | bảng giữa, x +0,22, yRel −0,2 |

Từ mắt phi công nhìn qua glareshield chúc xuống khoảng 12–20°: kiểm tra bằng ảnh chụp.

### 7.2 Màn hình: atlas render texture (`AvionicsSystem`)

- **Một** RenderTexture 1024×1024 (lưới 2×2 ô 512 px): ô 0 PFD, ô 1 ND, ô 2 E/WD, ô 3 SD. Màn của FO **dùng lại** ô PFD/ND của CPT (dữ liệu giống nhau khi bình thường). Mục M4: thêm ô MCDU (nâng lên 1536×1024).
- RT: `ARGB32`, có mipmap + auto generate (đỡ nhấp nháy khi nhìn xa), `filterMode = Trilinear`, `anisoLevel = 4`, không depth.
- Canvas uGUI dạng World Space đặt xa gốc (root riêng, không phải con của máy bay), layer `Avionics`. Một camera orthographic chỉ cull layer Avionics, `enabled = false`.
- Render chủ động theo tần số: `UniversalRenderPipeline.SingleCameraRequest { destination = rt }` + `RenderPipeline.SubmitRenderRequest`. Dự phòng: bật camera đúng 1 frame. Tần số: High **30 Hz**, Medium 20, Low 12. Chỉ render khi camera ở buồng lái hoặc khi trên màn hình (ngoài view) có DU hiển thị. Ở góc nhìn ngoài thì dừng hẳn.
- Mesh DU: quad có UV = ô atlas. Mọi DU dùng **một** material instance tạo lúc chạy (`new Material(M_Display)`, `_EmissionMap = rt`), không sửa asset gốc.
- `M_Display`: URP/Lit, base đen, smoothness 0,85 (kính bóng), emission trắng × `brightness` (núm độ sáng DU sau này).
- Chữ: giai đoạn đầu dùng `UnityEngine.UI.Text` với font built-in `LegacyRuntime.ttf` để không cần import TMP Essentials. G9 chuyển sang TextMeshPro SDF với font đơn cách kiểu avionics. Chỉ gán `.text` khi giá trị nguyên thay đổi (cache int), tránh tạo rác (GC).
- Băng số (speed/alt/heading) dùng `TapeScale` **có pool**: chỉ khoảng 12–20 vạch/nhãn được đặt lại vị trí mỗi lần render, không sinh hàng trăm phần tử.
- Màu Airbus (`AirbusColors`): xanh lá (0; 0,9; 0,2), cyan (0; 0,85; 1), magenta (1; 0,25; 1), amber (1; 0,62; 0), vàng (1; 1; 0), trời (0,11; 0,53; 0,93), đất (0,55; 0,33; 0,15).

**PFD** (không gian 512×512, gốc ở giữa):

| Thành phần | Vị trí / chi tiết |
|---|---|
| Chân trời | mask 250×260 tại (−10; 40). Nội dung 1200×1200: trời, đất, vạch chân trời. Thang pitch 2,5°/5°/10° với 6,5 px/độ. Xoay `z = +roll`, dịch `(0; −pitch·k)` đã xoay theo roll |
| Ký hiệu máy bay | thanh vàng cố định. Thang roll 0/10/20/30/45, kim vàng xoay theo roll |
| Băng tốc độ | (−195; 40), 70×260, bước 10 kt, nhãn mỗi 20, 3,5 px/kt. Mốc V1 "1" màu cyan, tốc độ mục tiêu tam giác magenta, mũi tên xu hướng vàng, Mach dưới băng khi > 0,5 |
| Băng độ cao | (175; 40), bước 100 ft, nhãn mỗi 500 ft (đơn vị trăm), 0,13 px/ft. Ô đọc số viền vàng. Độ cao chọn màu cyan phía trên, QNH màu cyan phía dưới |
| Tốc độ thẳng đứng | chữ số trăm fpm màu xanh khi \|VS\| > 200 |
| Hướng bay | (−10; −160), 230×40, bước 5°, nhãn mỗi 10°, vạch giữa vàng, hướng chọn tam giác cyan |
| FMA | 5 cột ở y = 215, đọc từ bus |
| Radio altitude | số xanh dưới chân trời khi RA < 2500 |

**ND (ROSE NAV):** vòng tròn r = 180 xoay theo `heading`, vạch 5°/10°, nhãn mỗi 30°. Ký hiệu máy bay vàng ở giữa, vạch chuẩn vàng ở trên. GS/TAS góc trên trái. Hướng chọn cyan, hình thoi track màu xanh. Vòng tầm r = 90. M4: chế độ ARC và vẽ đường bay FMC.

**E/WD:** 2 đồng hồ N1 (cung `Image.Filled` với sprite vòng tròn sinh thủ tục, kim, số). EGT (amber khi nóng), FF, chỉ báo FLAP (S/F + chữ CONF), FOB, vùng memo xanh.
**SD:** trang DOOR: sơ đồ đơn giản, cửa màu amber khi mở. Dải dưới: TAT/SAT/UTC/GW.

### 7.3 Tương tác và highlight cho tutorial

- `CockpitControl { id, kind (Button/Toggle/Knob/Lever), collider }`. Raycast từ camera khi chạm (bỏ qua nếu chạm trúng UI). `CockpitControl.Pressed` là event tĩnh, chỉ phát `id`.
- `Highlight(bool)`: emission nhấp nháy bằng MaterialPropertyBlock. Đây là cơ chế "sáng nút cần bấm" cho mức Người mới (idea.md mục 08).
- Id ban đầu: `FCU_SPD`, `FCU_HDG`, `FCU_ALT`, `FCU_VS`, `TL_1`, `TL_2`, `FLAP_LEVER`, `SPDBRK`, `GEAR_LEVER`, `PARK_BRK`, `OVHD_BAT1`, `OVHD_BAT2`, `OVHD_APU_MASTER`, `OVHD_APU_START`, `OVHD_BEACON`, `OVHD_NAV`, `OVHD_STROBE`, `OVHD_LAND`, `OVHD_SEATBELT`.
- Collider dạng hộp, nới to để dễ chạm (≥ 9 mm trên màn hình ở FOV mặc định).

### 7.4 Kính và đêm

- Lỗ kính để trống (không có mặt kính) ở MVP. Mục M6: thêm quad kính trong suốt, tint nhẹ, có phản chiếu.
- Đêm: viền emissive (integral lighting) trên các panel, dùng texture mask emission. Đèn flood buồng lái giả bằng ambient nội thất (mục 9.2).

---

## 8. Khoang khách

| Hạng mục | Thông số |
|---|---|
| Vỏ trong | s ∈ [4,3; 32,0], lùi 10 cm, texture 2048×1024 có alpha lỗ cửa sổ (chung luật với livery), vùng cửa sơn xám nhạt |
| Sàn | yRel −0,70, thảm `cabinCarpetColor`, lối đi ±0,26 màu khác (submesh) |
| Bố trí | 180Y: 30 hàng × 6 ghế (3-3), pitch 0,737 m (29"). Hàng 1 ở s = 7,6. Sau hàng 11 và 12 cộng thêm 0,12 m (hàng thoát hiểm trên cánh) |
| Ghế | đệm ngồi 0,44 × 0,46 cao 0,42, tựa lưng ngả 10°, bọc tựa đầu màu `cabinHeadrestColor`, tay vịn, chân ghế. Khoảng 180 tam giác mỗi ghế ba. Tâm ghế x = ±0,51 / ±0,97 / ±1,43 (C-D / B-E / A-F) |
| Gộp mesh | 3 khối (hàng 1–10, 11–20, 21–30), mỗi khối 3 submesh (vải, tựa đầu, khung): khoảng 9 draw call, ~11k tam giác |
| Hộc hành lý | profile đùn dọc s 7,2 → 31, đáy ở sàn + 1,62 |
| Trần | cung ở sàn + 2,15 |
| Đèn hắt | dải emissive `cabinLightColor` trên mép hộc |
| Bếp / toilet | hộp ở s 4,3–7,0 và 30,0–32,0 |
| Camera | `Cam_Cabin_12A` (ghế cửa sổ trái nhìn ra cánh), `Cam_Cabin_20F`, `Cam_Cabin_Aisle` (hàng 1 nhìn về sau). Mắt ở sàn + 1,12 |

Màu ghế mặc định đỏ/vàng chỉ là ước lượng, chỉnh trong `LiveryDefinition`.

---

## 9. Ánh sáng và môi trường

### 9.1 Ngoài trời

- Một Directional Light (mặt trời) realtime có bóng. `SunController`: giờ trong ngày → góc nâng/phương vị, cường độ, màu (ấm khi thấp), `NightFactor`.
- Bầu trời: `Skybox/Procedural` (instance lúc chạy để không sửa asset). M6: skybox HDRI nén ASTC.
- Ambient: tự tính `SphericalHarmonicsL2` từ 3 màu trời/xích đạo/đất rồi gán `RenderSettings.ambientProbe`. Không phụ thuộc bake.
- Phản chiếu: một Reflection Probe **baked** tại sân đỗ (Lighting > Generate). Chạy menu bake một lần cho scene sandbox. Không dùng probe realtime trên mobile.
- Sương mù: Linear fog nhẹ để giấu biên địa hình.

### 9.2 Nội thất trên máy bay đang di chuyển

Vấn đề: máy bay là vật động, nên lightmap tĩnh và light probe của thế giới không dùng được cho nội thất. Cách xử lý theo thứ tự:

1. **MVP: ambient riêng cho nội thất (`InteriorAmbient`).** Mọi renderer nội thất đặt `lightProbeUsage = CustomProvided`, MaterialPropertyBlock `CopySHCoefficientArraysFrom` với SH ấm tính từ `cabinLightColor` (đèn trần) + lấp sáng. Ngày/đêm và cờ `Cabin` đổi cường độ. Ngoại thất nhìn qua cửa sổ vẫn dùng ambient thế giới.
2. **Nắng qua cửa sổ:** vỏ trong đổ bóng **TwoSided** (`ShadowCastingMode.TwoSided`) và có lỗ alpha-clip. Ở góc nhìn trong, vỏ ngoài bị loại khỏi culling mask (layer ExteriorHull) **và** tắt đổ bóng (làm cả hai cho chắc), nên nắng chiếu thành vệt qua cửa sổ.
3. **M6:** bake AO và ánh sáng trần vào texture trong Blender (UV1), hoặc dùng mẫu `PrefabLightmapData` (bake trong scene riêng rồi gán `lightmapIndex/scaleOffset` lúc chạy).
4. Phản chiếu nội thất: smoothness thấp (0,2–0,35). DU và kính được bóng.

### 9.3 Post-processing (Volume toàn cục)

| Mức | Hiệu ứng |
|---|---|
| Low | tắt (camera `renderPostProcessing = false`) |
| Medium | Tonemapping Neutral + Color Adjustments |
| High (Turbo 4 Pro) | ACES + Color Adjustments + Bloom (threshold 1,0, intensity 0,35, downscale Quarter, high quality filtering tắt). Mục đích: hào quang đèn, màn hình DU, đèn đường băng. **Không** dùng SSAO, DoF, Motion Blur |

Camera đặt `antialiasing = None` khi đã dùng MSAA.

---

## 10. Camera (`CameraDirector`)

Chỉ dùng **một** camera chính. Director đổi vị trí, culling mask, near/far và FOV theo chế độ:

| Chế độ | Culling | Near / Far | Điều khiển |
|---|---|---|---|
| Exterior (orbit) | tất cả trừ Avionics, Interior | 0,3 / 30000 | kéo để xoay, pinch/cuộn để zoom 15–300 m, không xuống dưới mặt đất, damping |
| Cockpit | tất cả trừ Avionics, ExteriorHull | 0,05 / 15000 | nhìn quanh: yaw ±150°, pitch −70..+80°. Zoom bằng FOV 30–75, mặc định 62. Preset: phía trước, overhead, pedestal, cửa sổ bên. Nghiêng đầu ra cửa sổ ±0,1 m |
| Cabin | như Cockpit | 0,05 / 15000 | chuyển giữa các anchor `Cam_Cabin_*`, nhìn quanh |
| Tower | như Exterior | 0,5 / 30000 | đứng tại điểm tháp, nhìn theo máy bay, FOV tự co để máy bay chiếm ~30% khung hình |

- Đổi chế độ thì cập nhật đổ bóng vỏ ngoài (mục 9.2) và báo `AvionicsSystem` (dừng/chạy render).
- Nghe sự kiện `FloatingOrigin.Shifted` để dịch các vị trí đã cache (damping).
- Input qua `SkyInput` (chuột/cảm ứng/phím, hai backend). Bỏ qua cử chỉ bắt đầu trên UI.

---

## 11. Mức chất lượng và tối ưu cho Turbo 4 Pro

### 11.1 Ba URP asset (tạo bằng menu setup, chỉnh qua `SerializedObject`)

Nếu dự án đã có URP asset (template Universal 3D), nhân bản nó. Không có thì `UniversalRenderPipelineAsset.Create(rendererData)`. Dùng chung một `UniversalRendererData` (Forward).

| Thiết lập | Low | Medium | **High (Turbo 4 Pro)** |
|---|---|---|---|
| Render scale | 0,65 | 0,75 | **0,80 (động 0,70–0,90)** |
| MSAA | tắt | 2x | **4x** |
| HDR | tắt | tắt | **bật**, precision 32-bit (R11G11B10) |
| Main light shadow | 1024 | 2048 | **2048** |
| Cascades / khoảng cách | 1 / 60 m | 2 / 120 m | **2 / 200 m** (split 0,2) |
| Soft shadows | tắt | tắt | **bật (Low quality)** |
| Additional lights | per-vertex 1 | per-pixel 2 | **per-pixel 4**, không bóng |
| Depth / Opaque texture | tắt | tắt | **tắt** (bật thì Adreno phải resolve tile) |
| SRP Batcher | bật | bật | **bật** |
| Dynamic batching | tắt | tắt | tắt |
| Store Actions Optimization | Auto | Auto | **Auto** |
| LUT size | 16 | 32 | 32 |
| LOD bias | 0,7 | 1,0 | **1,5** |
| Texture mip limit | 1 (nửa) | 0 | 0 |
| Anisotropic | tắt | per-texture | per-texture |
| Target fps | 30 | 30 | **60** |
| Avionics Hz | 12 | 20 | **30** |
| Light thật của máy bay | tắt | landing | **landing + taxi + turnoff** |

Trường SerializedObject cần chỉnh (kiểm tra null rồi log nếu phiên bản URP đổi tên): `m_RenderScale`, `m_MSAA`, `m_SupportsHDR`, `m_HDRColorBufferPrecision`, `m_MainLightShadowmapResolution`, `m_ShadowDistance`, `m_ShadowCascadeCount`, `m_Cascade2Split`, `m_SoftShadowsSupported`, `m_AdditionalLightsRenderingMode`, `m_AdditionalLightsPerObjectLimit`, `m_AdditionalLightShadowsSupported`, `m_RequireDepthTexture`, `m_RequireOpaqueTexture`, `m_UseSRPBatcher`, `m_SupportsDynamicBatching`, `m_StoreActionsOptimization`, `m_ColorGradingLutSize`.

QualitySettings (`ProjectSettings/QualitySettings.asset`, mảng `m_QualitySettings`): đặt đúng 3 mức `Low/Medium/High`, mỗi mức có `customRenderPipeline`, `lodBias`, `globalTextureMipmapLimit`, `anisotropicTextures`, `vSyncCount = 0`, `antiAliasing = 0`. Mức mặc định cho Android (`m_PerPlatformDefaultQuality`) = **High**, vì máy thử là Turbo 4 Pro.

### 11.2 Player Settings Android

- Graphics API: **Vulkan**, rồi OpenGLES3 (tắt Auto Graphics API). Color space Linear.
- Scripting IL2CPP, ARM64 only, Min API 29 (Android 10), Target API mới nhất có sẵn.
- Texture compression mặc định ASTC. *Optimized Frame Pacing* bật. *Multithreaded Rendering* bật. Graphics Jobs bật (Vulkan, kiểm tra ổn định trên máy).
- Resolution: *Render Outside Safe Area* bật. Không giới hạn độ phân giải bằng Player Settings, việc đó đã do render scale lo.
- `Application.targetFrameRate = 60` khi High. HyperOS có thể khoá 60/120 Hz theo app: xác nhận bằng overlay đo fps của Developer options.

### 11.3 Render scale động (`GraphicsQualityManager`)

- Đo thời gian frame GPU bằng `FrameTimingManager` (bật *Frame Timing Stats* trong Player Settings). Lấy EMA 1 giây.
- Ngưỡng ở High: GPU > 13,5 ms trong 2 s thì giảm render scale 0,05. GPU < 9 ms trong 5 s thì tăng 0,05. Kẹp trong [0,70; 0,90].
- Chỉnh `UniversalRenderPipelineAsset.renderScale` **chỉ trên thiết bị** (`#if !UNITY_EDITOR`), hoặc khôi phục giá trị cũ khi thoát Play mode để không ghi bẩn asset.
- Trong buồng lái thì được giảm render scale mạnh hơn vì DU là RT riêng, không bị mờ theo.

### 11.4 Hạ cấp theo nhiệt

- Android 11+: `PowerManager.getThermalHeadroom(10)` qua `AndroidJavaObject`, gọi mỗi 5 s. Có thể thêm gói *Adaptive Performance* (provider Android/ADPF) nếu ổn định trên Unity 6.
- Headroom > 0,85 hoặc trạng thái ≥ SEVERE: bậc 1 = avionics 20 Hz, bóng 120 m. Bậc 2 = target 45 fps, MSAA 2x. Bậc 3 = chuyển tier Medium. Hồi lại khi headroom < 0,7 trong 60 s.
- Hiện biểu tượng nhỏ trên HUD debug khi đang hạ cấp.

### 11.5 Đo đạc (bắt buộc mỗi mốc)

- Build *Development* + *Autoconnect Profiler*. Đo 3 cảnh: (a) ngoài máy bay ở sân đỗ, (b) buồng lái nhìn qua kính khi cất cánh, (c) khoang khách nhìn ra cánh.
- Ghi vào `plan/perf-log.md`: fps trung bình, 1% low, GPU ms, CPU ms, SetPass, tam giác, bộ nhớ texture, nhiệt độ sau 15 phút.
- Công cụ: Unity Profiler, Frame Debugger, Memory Profiler. Snapdragon Profiler hoặc Android GPU Inspector cho GPU Adreno.
- Tiêu chí High trên Turbo 4 Pro: **≥ 58 fps trung bình, 1% low ≥ 50, sau 15 phút vẫn ≥ 55**.

---

## 12. Pipeline import asset (`ArtImportRules`, AssetPostprocessor)

Chỉ áp dụng cho `Assets/SkyGate/Art/` và chỉ khi `importSettingsMissing` (lần import đầu), để người dùng vẫn sửa tay được.

- **Model:** `globalScale = 1`, `useFileScale`, `bakeAxisConversion = true`, không import camera/light/visibility/blendshape, `isReadable = false`, `meshCompression = Low`, chỉ import animation khi tên chứa `_anim`, tangents MikkTSpace.
- **Kiểm tra sau import (thư mục `Aircraft/`):** đủ node bắt buộc (`AircraftPartNames.Required`), root rotation = identity, chiều dài theo Z nằm trong 30–45 m, đếm tam giác theo từng LOD so với ngân sách. In một dòng tóm tắt và cảnh báo.
- **Texture:** override Android ASTC: albedo 6x6, normal (`_N`/`_Normal`) 4x4 và sRGB tắt, mask (`_M`/`_AO`) 6x6 và sRGB tắt. Max size: `Aircraft/` 2048 (riêng livery thân 4096), `Vehicles/` và `People/` 1024, `Airport/` 2048. Mipmap bật.
- `Art/CREDITS.md`: mỗi model ghi nguồn, tác giả, giấy phép, link (idea.md mục 12).

---

## 13. Floating origin

- `FloatingOrigin`: khi máy bay cách gốc quá **2000 m** theo phương ngang, dịch mọi root object của các scene đã load đi `−(x, 0, z)`. Cộng dồn offset kiểu `double` để tính toạ độ địa lý. Gọi `Physics.SyncTransforms()`, rồi phát `Shifted(Vector3 delta)`.
- Không dịch trục Y, để mặt đất luôn ở y = 0.
- Hệ hạt và trail dùng world space phải nghe `Shifted` và dịch theo. Root avionics ở xa cũng bị dịch, nhưng vì camera atlas nằm cùng root nên không ảnh hưởng.

---

## 14. Danh sách task cho Sonnet

Mỗi task: viết code → người dùng chạy trong Editor → sửa lỗi → sang task sau. Gắn với mốc của idea.md trong ngoặc.

| # | Task | File chính | Xong khi |
|---|---|---|---|
| **G0** | Setup dự án (M0) | `Editor/ProjectSetup.cs`: menu `SkyGate/0. Thiết lập dự án`. Tạo layer, 3 URP asset, QualitySettings, Player Settings Android (mục 11) | Mở dự án không lỗi compile. Menu chạy xong in bảng tóm tắt. Build APK trống mở được trên Turbo 4 Pro ở 60 fps |
| **G1** | Core + hạ tầng | `Core/*` (đã có), `SkyLayers`, `MeshBuilder` (đã có), `Airfoil`, `FuselageProfile`, `A320Windows`, `BitmapFont5x7`, `AircraftPartNames`, `Editor/AssetIO` (lưu/ghi đè asset giữ GUID bằng `EditorUtility.CopySerialized`), `Editor/MaterialFactory` | Không lỗi compile. Một editor test nhỏ in tam giác/kích thước của mesh thân |
| **G2** | Ngoại thất placeholder | `Editor/A320ExteriorGenerator.cs` (mục 6.1, 3 LOD) | Mesh đúng kích thước mục 4, không mặt bị lật (kiểm tra bằng shading/backface) |
| **G3** | Livery | `Editor/LiveryBaker.cs`, `Data/Livery_VJ_Style.asset` | Texture thân và đuôi đúng chiều đọc hai bên. Cửa sổ và cửa đúng vị trí. Đổi màu trong asset rồi build lại thì cập nhật |
| **G4** | Nội thất placeholder | `Editor/A320InteriorGenerator.cs` (mục 7.1, 8) | Từ `Eye_CPT` nhìn thấy bảng, DU, kính đúng chỗ. Từ ghế 12A thấy cánh qua cửa sổ |
| **G5** | Prefab + menu build | `Editor/A320AssetBuilder.cs`: menu `SkyGate/1. Build A320 placeholder` → `Generated/Prefabs/A320_VJ.prefab` với LODGroup, layer, anchor | Chạy menu 2 lần không tạo trùng asset, GUID giữ nguyên |
| **G6** | Rig + đèn | `Aircraft/AircraftVisualRig.cs`, `PartBinding`, `A320RigDefaults`, `AircraftLights.cs`, `Demo/DemoStateDriver.cs` | Phím G/F/L/D/B/R làm càng, flap, đèn, cửa, speedbrake, reverser chuyển động đúng chiều cả hai bên |
| **G7** | Scene sandbox + camera | `Editor/SandboxSceneBuilder.cs` (mặt đất, đường băng 3200×45, sân đỗ, mặt trời, sky, volume, máy bay, hệ thống), `Cameras/*`, `Input/SkyInput.cs`, `Demo/DebugHud.cs` | 4 chế độ camera chạy bằng chuột và cảm ứng. HUD có nút đổi view, tier, ngày/đêm |
| **G8** | Ánh sáng nội thất | `Aircraft/InteriorAmbient.cs`, `Environment/SunController.cs` | Ban ngày có vệt nắng qua cửa sổ khoang. Ban đêm cabin ấm, ngoài tối |
| **G9** | Avionics | `Avionics/AvionicsSystem.cs`, `UIFactory`, `TapeScale`, `PfdDisplay`, `NdDisplay`, `EwdDisplay`, `SdDisplay`, `AirbusColors` (mục 7.2) | PFD phản ánh đúng pitch/roll/IAS/alt/hdg/V-speed từ DemoStateDriver. Render 30 Hz. Ở góc nhìn ngoài không render. Không phát sinh GC mỗi frame (kiểm tra Profiler) |
| **G10** | Tương tác buồng lái | `Cockpit/CockpitControl.cs`, `CockpitInteractor.cs` | Chạm cần càng thì phát `GEAR_LEVER`, demo đổi gear. `Highlight` nhấp nháy |
| **G11** | Quality + Turbo 4 Pro | `Quality/GraphicsQualityManager.cs` (tier, render scale động, nhiệt), `ArtImportRules.cs` | Đạt tiêu chí mục 11.5 trên Turbo 4 Pro, ghi `plan/perf-log.md` |
| **G12** | Floating origin | `Environment/FloatingOrigin.cs` | Bay vòng bán kính 5 km không rung hình, camera không giật khi dịch gốc |
| **G13** | Đường thay model thật (M6) | tài liệu `Art/Aircraft/A320/README.md` + chạy kiểm tra import | Một model thử (kể cả chỉ là bản sửa của placeholder xuất qua Blender) thay được và rig chạy không sửa code |
| **G14** | Hoàn thiện hình ảnh (M6) | quạt có blur disc, kính có phản chiếu, viền sáng panel ban đêm, đĩa xoay reverser, HDRI, đèn đường băng instanced | Ảnh chụp so sánh trước và sau. Fps không tụt quá 3 so với G11 |

Phím demo (`DemoStateDriver`): G càng, F flap, L đèn (theo chuỗi: parked → taxi → takeoff → cruise), D cửa, B speedbrake, R reverser, N ngày/đêm, P bay vòng/đỗ, C đổi camera, 1/2/3 đổi tier.

---

## 15. Quy ước code và lưu ý API

**Quy ước**

- Namespace theo thư mục: `SkyGate.Core`, `SkyGate.Rendering.*`, `SkyGate.Editor`. Comment tiếng Việt, tên định danh tiếng Anh.
- Không `Find`, `GetComponent` hay LINQ trong `Update`/`LateUpdate`. Cache trong `Awake`. Không cấp phát trong vòng lặp frame (mảng kênh, chuỗi, closure).
- Không sửa asset dùng chung lúc chạy (material, skybox, URP asset). Tạo instance hoặc dùng MaterialPropertyBlock.
- Generator Editor phải **lặp lại được**: chạy lại ghi đè asset nhưng giữ GUID, không tạo `Mesh 1`, `Mesh 2`.
- Mọi số liệu máy bay đặt trong `A320Data` hoặc ScriptableObject, không rải "số ma thuật" trong code rendering.
- Mỗi task kết thúc bằng đoạn "Cách kiểm tra" cho người dùng (bấm menu nào, nhìn gì).

**API phụ thuộc phiên bản (Unity 6 / URP 17)**

- `RenderPipeline.SubmitRenderRequest` + `UniversalRenderPipeline.SingleCameraRequest`: có từ Unity 2023.2. Luôn kiểm tra `RenderPipeline.SupportsRenderRequest`. Dự phòng: bật camera 1 frame.
- `GraphicsSettings.defaultRenderPipeline` (không dùng `renderPipelineAsset` đã obsolete).
- Thuộc tính private của URP asset đổi tên giữa các bản. Luôn chỉnh qua `SerializedObject` + `FindProperty` có kiểm tra null rồi log.
- Material URP/Lit tạo bằng code phải tự bật keyword: `_ALPHATEST_ON` (+ `_AlphaClip = 1`, `_Cutoff`), `_EMISSION`, `_SURFACE_TYPE_TRANSPARENT` (+ `_Surface = 1`, `_SrcBlend`, `_DstBlend`, `_SrcBlendAlpha`, `_DstBlendAlpha`, `_ZWrite = 0`, `renderQueue = 3000`), `_Cull = 0` cho hai mặt.
- Hào quang additive: `Universal Render Pipeline/Particles/Unlit`, `_Surface = 1`, `_Blend = 2`.
- Unity 6 đã gộp TextMeshPro vào `com.unity.ugui`. Lần đầu dùng phải import TMP Essentials, nên G9 dùng `Text` cũ trước.
- `LightProbeUsage.CustomProvided` trên URP: nếu G8 cho kết quả sai (nội thất đen hoặc giống hệt ngoài trời), dự phòng là đổi `RenderSettings.ambientProbe` theo chế độ camera.
- Layer bị loại khỏi culling mask thường không đổ bóng trong camera đó. Vẫn tắt `shadowCastingMode` của vỏ ngoài ở góc nhìn trong cho chắc.

---

## 16. Rủi ro và việc người dùng cần làm

| Rủi ro | Xử lý |
|---|---|
| Code viết mà không compile thử được | Chia task nhỏ. Người dùng gửi Console log sau mỗi task |
| Version gói trong `manifest.json` lệch bản Unity 6 đang cài | Unity tự nâng gói core (URP). Nếu lỗi: tạo dự án mới bằng template *Universal 3D*, chép `Assets/SkyGate` sang |
| Placeholder thủ tục trông "đồ chơi" | Chấp nhận ở M1–M5. Hợp đồng 5.2 đảm bảo thay model thật ở M6 không sửa code |
| Cửa mở nhưng vỏ không có lỗ | Placeholder ẩn tấm cửa khi đóng. Model thật phải có lỗ cửa + tấm cửa riêng (ống lồng ở M2 cần cái này) |
| Thương hiệu VietJet | Logo và chữ do người dùng tự cung cấp, để trong thư mục không commit, chỉ dùng phi thương mại |
| HyperOS khoá tần số làm tươi | Kiểm tra bằng overlay fps. Nếu bị khoá 60 Hz thì vẫn đạt mục tiêu |
| Máy nóng sau 15–20 phút | Hạ cấp theo nhiệt (11.4), đo lại ở G11 |

**Người dùng làm:** cài Unity 6 LTS + Android Build Support (OpenJDK, SDK, NDK). Mở `SkyGate/` bằng Unity Hub. Chạy menu theo thứ tự G0 → G7. Gửi log/ảnh. Chụp ảnh tham khảo livery VietJet thật để chỉnh màu. Tìm model A320 có giấy phép phù hợp cho M6 và ghi vào `CREDITS.md`.

### Câu hỏi mở (Sonnet ghi thêm khi gặp)

- Turbo 4 Pro bản 12 GB hay 16 GB? (ảnh hưởng ngân sách texture High).
- Có dùng tay cầm Bluetooth khi thử không? (ảnh hưởng `SkyInput`).

---

## 17. Trạng thái triển khai (cập nhật sau khi viết code)

Đã viết code cho G0 đến G12 trong `SkyGate/` (xem `SkyGate/README.md`). **Chưa compile, chưa chạy thử trong Unity.** Sai khác so với plan:

- **Điều khiển cảm ứng (yêu cầu thêm):** `Rendering/Input/TouchFlightControls.cs` + `Core/PilotControls.cs`. Cần ga hình cần gạt chữ T có nấc REV/IDLE/CLB/FLX/TOGA;
  yoke là vô lăng kéo-xoay trên màn hình hoặc nghiêng điện thoại (chế độ VÔ LĂNG / NGHIÊNG / CẢ HAI, có hiệu chỉnh ZERO); cần gear (UP/DN), flap 5 nấc, speedbrake.
  Tự xử lý đa chạm bằng `SkyInput` nên không cần EventSystem. Lớp input ghi `PilotInputBus.Current`, mô phỏng đọc; rendering chỉ đọc `AircraftVisualState`.
- **Phản chiếu:** dùng skybox làm nguồn phản chiếu (`DefaultReflectionMode.Skybox` + `DynamicGI.UpdateEnvironment` khi đổi giờ) thay cho Reflection Probe bake: rẻ hơn, không cần bake.
- **Input:** namespace `SkyGate.Rendering.Controls` (không dùng tên `Input` để khỏi trùng `UnityEngine.Input`). Project Settings đặt Active Input Handling = Both để có cả Input System (chạm, phím) lẫn cảm biến nghiêng của Input cũ.
- **Chưa làm:** G13 chỉ có tài liệu thay model (`Art/Aircraft/A320/README.md`) và kiểm tra import; G14 (blur disc quạt, kính phản chiếu, viền sáng panel ban đêm, HDRI, đèn đường băng instanced) để sau khi G11 đạt tiêu chí fps.
- **Cần kiểm tra trên máy:** dấu/chiều nghiêng điện thoại (roll, pitch), tên thuộc tính URP asset theo phiên bản, hình dạng livery so với ảnh VietJet thật, `FrameTimingManager` có số liệu GPU trên Adreno hay không.
