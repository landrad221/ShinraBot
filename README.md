# 🌸 ShiraBot

[![Build Status](https://img.shields.io/github/actions/workflow/status/shirabot/shirabot/dotnet-ci.yml?branch=main&style=for-the-badge)](https://github.com/shirabot/shirabot/actions)
[![License](https://img.shields.io/badge/License-MIT-deeppink.svg?style=for-the-badge)](LICENSE)
[![Discord](https://img.shields.io/discord/1234567890?color=7289da&label=Support%20Server&logo=discord&style=for-the-badge)](https://discord.gg/shirabot)
[![Framework](https://img.shields.io/badge/.NET-8.0-512bd4.svg?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)

**ShiraBot** is a high-performance, multipurpose Discord bot written in **C#** using **Discord.Net**. Designed for modern communities, it features a dynamic leveling system and robust moderation tools, optimized for Linux environments and containerized deployments.

[Invite ShiraBot](https://discord.com/api/oauth2/authorize?client_id=YOUR_ID&permissions=8&scope=bot) • [Features](#-features) • [Deployment](#-deployment) • [Commands](#-commands) • [Contributing](#-contributing)

---

## ✨ Features

### 📈 Dynamic Leveling System
* **XP Scaling:** Advanced algorithms for fair experience gain.
* **Rank Cards:** Customizable image-based rank cards (rendered via SkiaSharp).
* **Role Rewards:** Automatically assign roles when users reach specific levels.
* **Database Support:** Native support for PostgreSQL and SQLite.

### 🛡️ Robust Moderation
* **Auto-Mod:** Smart filters for spam, mass-mentions, and blacklisted words.
* **Logging:** Detailed audit logs for deleted messages and member changes.
* **Hard Moderation:** Commands for `kick`, `ban`, `mute`, and `timeout`.

### ⚙️ Core Essentials
* **Slash Commands:** Fully utilizes Discord's Interaction framework.
* **Dependency Injection:** Built on `Microsoft.Extensions.DependencyInjection` for modularity.
* **Linux Optimized:** Designed specifically for high-uptime Linux performance.

## 📦 Deployment

### 🐳 Docker (Recommended)
The easiest way to deploy ShiraBot is via Docker.

```bash
# Clone the repository
git clone https://github.com/shirabot/shirabot.git
cd shirabot

# Setup environment variables
cp .env.example .env

# Spin up the containers (includes PostgreSQL and the Bot)
docker-compose up -d
```

### 🐧 Manual Linux Setup
Requires **.NET 8.0 SDK** or Runtime.

1.  **Clone:** `git clone https://github.com/shirabot/shirabot.git`
2.  **Publish:**
    ```bash
    dotnet publish -c Release -r linux-x64 --self-contained
    ```
3.  **Configure:** Update `appsettings.json` with your bot token and connection string.
4.  **Run:** `./bin/Release/net8.0/linux-x64/publish/ShiraBot`

## 🛠️ Commands

| Category | Command | Description |
| :--- | :--- | :--- |
| **Leveling** | `/rank` | Display your current level and XP. |
| **Mod** | `/ban` | Ban a member from the server. |
| **Utility** | `/ping` | Check the bot's heartbeat latency. |

## 🤝 Contributing

ShiraBot is an open-source project. We use standard C# coding conventions.

1.  **Fork** the repository.
2.  Create a **feature branch** (`git checkout -b feature/NewFeature`).
3.  **Commit** your changes.
4.  **Push** to the branch and open a **Pull Request**.

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

---

<p align="center">
  <b>Built with 💜 and .NET</b>
</p>
