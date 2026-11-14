# SkyGate: project Unity (đồ họa A320, Unity 6 + URP)

Code được viết theo `../plan/graphics-technical-plan.md` nhưng **chưa được compile** (môi trường viết code không có Unity).
Lần đầu mở có thể có lỗi compile nhỏ: copy nội dung Console gửi lại để sửa.

## Mở dự án
1. Unity Hub > Add project from disk > chọn thư mục `SkyGate/` (Unity 6 LTS, thêm Android Build Support).
   Nếu gói trong `Packages/manifest.json` báo lệch phiên bản, để Unity tự nâng cấp. Nếu vẫn lỗi: tạo dự án mới từ template
   *Universal 3D*, rồi chép thư mục `Assets/SkyGate` vào.
2. Menu **SkyGate > 0. Thiết lập dự án** (layer, 3 URP asset, Quality, Player Settings Android, Input = Both). Khởi động lại Editor nếu được hỏi.
3. Menu **SkyGate > 1. Build A320 placeholder** (bake sơn, dựng mesh, tạo prefab `Generated/Prefabs/A320_VJ.prefab`). Mất vài chục giây.
4. Menu **SkyGate > 2. Build Graphics Sandbox scene**, mở `Scenes/GraphicsSandbox.unity`, bấm Play.

## Điều khiển
Trên điện thoại (landscape):
- **Cần ga** (trái dưới): kéo cần chữ T. Nấc REV / IDLE / CLB / FLX-MCT / TOGA, có hút nấc. REV chỉ dùng được khi ở mặt đất.
- **Yoke** (phải dưới): kéo trên vòng tròn để xoay vô lăng (ngang = roll, dọc = pitch, tự về giữa). Kéo xuống = kéo cần (mũi lên).
- **Nghiêng điện thoại**: nút `YOKE` trên thanh trên đổi chế độ VÔ LĂNG / NGHIÊNG / CẢ HAI. `ZERO TILT` lấy tư thế đang cầm làm vị trí giữa.
  Nghiêng như xoay vô lăng = roll; ngả đỉnh màn hình về phía người = kéo cần. Nếu ngược chiều, vào **CÀI ĐẶT** (góc trên bên phải) và bật `PITCH: ĐẢO` hoặc `CHIỀU NGHIÊNG: ĐẢO`.
  CÀI ĐẶT có: chọn YOKE = cần/vô lăng trên màn hình, nghiêng điện thoại, hoặc cả hai; đảo pitch; đảo chiều nghiêng; độ nhạy nghiêng (thấp/vừa/cao); ZERO TILT. Các lựa chọn được lưu (PlayerPrefs) và nhớ lần sau.
  Ở chế độ NGHIÊNG, vô lăng trên màn hình chỉ để hiển thị (không nhận chạm). Cần ga, gear, flap, speedbrake luôn là cần trên màn hình.
- **Gear** (cần UP/DN), **Flaps** (5 nấc), **Speedbrake**: kéo cần. Đèn gear: xanh = thả hết, đỏ = đang chuyển.
- Nút trên cùng: CAM (đổi góc nhìn), CAM+ (camera phụ/preset), ĐỒ HỌA (Low/Medium/High), NGÀY/ĐÊM.
Trong Editor: Shift/Ctrl cần ga, mũi tên hoặc W/S lái, G gear, F flap (giữ Z để lùi), B speedbrake, L đèn, D cửa, N ngày/đêm, P dừng/chạy, C/V camera.

`DemoStateDriver` chỉ là mô phỏng đồ chơi để thử hình ảnh. Mô hình bay thật sẽ ghi `AircraftVisualState` thay nó và đọc `PilotInputBus.Current`.

## Test và CI
Xem `Assets/SkyGate/Tests/README.md`. EditMode/PlayMode chạy trong **Window > General > Test Runner**.
CI (GitHub Actions, thư mục `.github/` ở gốc repo): test + coverage (`SkyGate.Core` >= 95%) và quét bảo mật gitleaks/Trivy/Semgrep.

## Cấu trúc
- `Scripts/Core`: dữ liệu thuần (AircraftVisualState, A320Data, LiveryDefinition, PilotControls).
- `Scripts/Rendering`: mọi thứ chạy lúc Play (rig, đèn, avionics, camera, điều khiển cảm ứng, quality, môi trường).
- `Scripts/Editor`: generator mesh/livery/prefab/scene và menu setup.
- `Generated/`: asset do generator tạo, đừng sửa tay (chạy lại menu sẽ ghi đè, giữ GUID).
- `Art/`: model/texture thật sau này, ghi nguồn vào `Art/CREDITS.md`.
