# Tests

> Các test này **chưa được compile hay chạy** (môi trường viết code không có Unity). Lần đầu mở Test Runner có thể có lỗi
> compile nhỏ, nhất là các lời gọi `InputTestFixture` (tên tham số có thể khác theo phiên bản Input System). Gửi lại nội dung Console để sửa.

## Chạy trong Editor
**Window > General > Test Runner**, chọn tab *EditMode* hoặc *PlayMode*, bấm *Run All*.

| Thư mục | Chạy ở đâu | Nội dung |
|---|---|---|
| `EditMode/` | Editor, không cần Play | `SkyGate.Core` (A320Data, AircraftVisualState, PilotControls, LiveryDefinition, AircraftStateBus), ánh xạ cần ga, đường cong yoke, Airfoil, SkyLayers |
| `PlayMode/` | Editor ở chế độ Play | `SkyInput` (thiết bị ảo), `TouchFlightControls` (chạm thật vào từng cần/vô lăng), `DemoStateDriver`, `GraphicsQualityManager`, smoke test scene |

`Smoke` (`SandboxSceneSmokeTests`) tự **bỏ qua** nếu chưa có `Scenes/GraphicsSandbox.unity`. Chạy menu *SkyGate > 2. Build Graphics Sandbox scene* rồi commit scene để bật nó.

## Quy ước viết test
- Test giao diện cảm ứng: tính toạ độ từ `RectTransform` (xem `TouchFlightControlsTests.At/TrackTop`), không hard-code pixel.
  Cảm ứng ảo dùng `Input.BeginTouch/MoveTouch/EndTouch(..., queueEventOnly: true)` rồi `yield return null`, để `wasPressedThisFrame`
  còn đúng trong `Update` của frame kế tiếp.
- Test gọi trực tiếp `SkyInput` thì **không** dùng `queueEventOnly` (cần cập nhật ngay).
- Test sửa trạng thái toàn cục (`PilotInputBus`, `QualitySettings`, `PlayerPrefs`...) phải trả lại trong `TearDown`.
- Khi đổi tên một đối tượng UI trong `TouchFlightControls.BuildUi` (`Throttle`, `Yoke`, `Gear`, `Flap`, `Speedbrake`, `Settings`, `Btn_*`), cập nhật đường dẫn trong test.

## Coverage
- Chạy cục bộ: Test Runner > menu *Enable Code Coverage* (package Code Coverage), rồi *Window > Analysis > Code Coverage*.
- CI (`.github/workflows/tests.yml`) **chặn merge nếu line coverage của `SkyGate.Core` < 95%**.
  `SkyGate.Rendering` được báo cáo nhưng không bị chặn: phần lớn là uGUI, avionics và mesh procedural; ép 95% ở đó sẽ sinh test vô nghĩa.
  Muốn nâng ngưỡng cho Rendering: tách logic thuần ra khỏi MonoBehaviour (vd. `TouchFlightControls`, `DemoStateDriver`) vào assembly `SkyGate.Core`, rồi viết test EditMode cho nó.

## Những gì test KHÔNG thay thế được
Nghiêng điện thoại (accelerometer thật), đa chạm bằng hai ngón thật, FPS/nhiệt trên Redmi Turbo 4 Pro. Vẫn phải thử trên máy.

## CI cần gì
1. Mở dự án trong Unity một lần rồi commit `ProjectSettings/` và các file `.meta` (CI đọc phiên bản Unity từ `ProjectSettings/ProjectVersion.txt`).
2. Secrets của repo: `UNITY_LICENSE` + `UNITY_EMAIL` + `UNITY_PASSWORD` (Personal) hoặc `UNITY_SERIAL` + `UNITY_EMAIL` + `UNITY_PASSWORD` (Pro). Xem https://game-ci.com/docs/github/activation
3. (Chỉ nếu repo thuộc Organization) secret `GITLEAKS_LICENSE`.
