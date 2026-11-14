# Thay model A320 thật (không sửa code)

Model thật phải tuân thủ "hợp đồng model" trong `plan/graphics-technical-plan.md` mục 5.2:

1. Đơn vị mét, mũi hướng +Z, lên +Y, root rotation = identity, scale = 1. Blender: xuất FBX với *Apply Transform*.
2. Tên node đúng như `AircraftPartNames` (Fuselage, Wing_L/R, FlapIn/Out_L/R, Aileron_L/R, Elevator_L/R, VStab, Rudder,
   Engine_1/2, Fan_1/2, Reverser_1/2, MainGear_L/R(+_Wheels), NoseGear/_Steer/_Wheels, Door_*, Interior/Cockpit/Cabin, Anchors/Eye_CPT...).
3. Pivot ở bản lề/trục quay; trục X cục bộ của bề mặt điều khiển trùng đường bản lề, hướng +X.
4. LOD: mesh con `<Node>_LOD0/1/2`. Ngân sách tam giác: 60–90k / 25k / 6k.
5. Material theo slot (A320_Fuselage, A320_Tail, ... Cabin_Seat...).

Sau khi import, Console in báo cáo kiểm tra (đủ node, kích thước, tam giác). Kéo model vào scene, thêm
`AircraftStateBus`, `AircraftVisualRig` (bấm Reset để nạp binding mặc định), `AircraftLights`, `InteriorAmbient`, `AvionicsSystem`,
rồi chỉnh trục/dấu từng binding trong Inspector nếu pivot khác quy ước.
Ghi nguồn và giấy phép vào `../../CREDITS.md`.
