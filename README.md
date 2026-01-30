# Edungeon - Multiplayer Quiz Game

**Author:** Phan Hoàng Sơn  
**Project:** Real-time Multiplayer Dungeon Crawler & Quiz Game

## 📖 Giới thiệu
Edungeon là một tựa game giáo dục kết hợp giải trí, nơi người chơi cùng nhau khám phá hầm ngục và trả lời các câu hỏi trắc nghiệm để tiêu diệt quái vật.

## 🚀 Tính năng chính
-   **Multiplayer Real-time:** Kết nối nhiều người chơi cùng lúc (TCP/WebSocket).
-   **Hệ thống Quiz:** Trả lời câu hỏi để tấn công quái vật.
-   **Leaderboard:** Bảng xếp hạng cập nhật thời gian thực.
-   **Chat System:** Trò chuyện trực tuyến trong phòng.
-   **Cross-platform:** Hỗ trợ chơi trên PC và WebGL.

## 🛠️ Cài đặt & Chạy dự án

### 1. Server (Máy chủ)
Để chơi được mode Online, cần phải bật Server trước.
1.  Vào thư mục: `Server/Server_Edungeon_Unity_C#`
2.  Mở Terminal (CMD/PowerShell) tại đó.
3.  Chạy lệnh:
    ```bash
    dotnet run
    ```
4.  Khi thấy dòng `[Server] TCP Started on port 7781` là thành công.

### 2. Client (Người chơi)
1.  Mở dự án bằng **Unity Hub** (Add project từ thư mục `Client`).
2.  Mở Scene: `Assets/Scenes/Home.unity`.
3.  Nhấn **Play** để bắt đầu.
4.  Nhập Tên và Room ID để vào phòng.

## 📂 Cấu trúc dự án
-   **Client/**: Source code Unity (C# Scripts, Assets, Prefabs).
-   **Server/**: Source code Server (C# .NET Console App).
-   **Team_Assignments.csv**: Bảng phân công công việc.
-   **pseudo_code_report.md**: Mã giả giải thuật của dự án.

---
*© 2026 Phan Hoàng Sơn. All rights reserved.*
