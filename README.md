# 🌸 ShiraBot

[![Build Status](https://img.shields.io/github/actions/workflow/status/shirabot/shirabot/dotnet-ci.yml?branch=main&style=for-the-badge)](https://github.com/shirabot/shirabot/actions)
[![License](https://img.shields.io/badge/License-MIT-deeppink.svg?style=for-the-badge)](LICENSE)
[![Discord](https://img.shields.io/discord/1234567890?color=7289da&label=Support%20Server&logo=discord&style=for-the-badge)](https://discord.gg/shirabot)
[![Framework](https://img.shields.io/badge/.NET-8.0-512bd4.svg?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)

**ShiraBot** is a multipurpose Discord bot written in **C#** using **DSharpPlus**. It includes a dynamic leveling system, server-configurable rewards, automatic moderation, and moderation tools, with support for Linux and containerized deployments.

[Invite ShiraBot](https://discord.com/api/oauth2/authorize?client_id=YOUR_ID&permissions=8&scope=bot) • [Features](#-features) • [Deployment](#-deployment) • [Commands](#-commands) • [Contributing](#-contributing)

---

## ✨ Features

### 📈 Dynamic Leveling System
* **XP Scaling:** Earn 15-25 XP from messages, with a 60-second per-user cooldown. Levels follow `floor(sqrt(XP / 100))`.
* **Rank Cards:** `/rank` renders a PNG card with the member's current level, XP, and progress to the next level.
* **Role Rewards:** Configure automatic level rewards with `/reward-role`.
* **Database Support:** PostgreSQL and SQLite, configured through environment variables. SQLite is the default for local runs.

### 🛡️ Robust Moderation
* **Auto-Mod:** Removes bursts of six messages in eight seconds, mass mentions, and server-configured blocked words.
* **Logging:** Sends deleted-message and member-join/leave logs to the channel configured with `MOD_LOG_CHANNEL_ID`.
* **Hard Moderation:** Slash commands `/kick`, `/ban`, `/mute`, and `/timeout`, guarded by Discord permissions.

### ⚙️ Core Essentials
* **Slash Commands:** Uses Discord interactions for `/rank`, `/ping`, moderation, and server configuration.
* **Dependency Injection:** Built on `Microsoft.Extensions.DependencyInjection`.
* **Linux Optimized:** Includes Linux-native SkiaSharp assets for rank card rendering.

## 📦 Deployment

### 🐳 Docker (Recommended)
The easiest way to deploy ShiraBot is via Docker.

```bash
# Clone the repository
git clone https://github.com/shirabot/shirabot.git
cd shirabot

# Setup environment variables
cp .env.example .env
# Set BOT_DISCORD_TOKEN and replace the example PostgreSQL password.
# Enable the Message Content and Server Members privileged intents in the Discord Developer Portal.
# Grant the bot the permissions required by the commands and moderation filters.

# Start PostgreSQL and the bot
docker-compose up -d
```

### 🐧 Manual Linux Setup
Requires the **.NET 9 SDK** or Runtime.

1.  **Clone:** `git clone https://github.com/shirabot/shirabot.git`
2.  **Publish:**
    ```bash
    dotnet publish -c Release
    ```
  3.  **Configure:** Set `BOT_DISCORD_TOKEN`. The bot defaults to SQLite at `db/level.db`; set `DATABASE_PROVIDER=postgres` and `DATABASE_CONNECTION_STRING` to use PostgreSQL instead. Set `MOD_LOG_CHANNEL_ID` to enable moderation logs.
  4.  **Run:** `dotnet bin/Release/net9.0/ShinraBot.dll`

## 🛠️ Commands

| Category | Command | Description |
| :--- | :--- | :--- |
| **Leveling** | `/rank` | Display your current level, XP, and rank card. |
| **Leveling** | `/reward-role` | Assign a role reward to a level (Manage Server). |
| **Mod** | `/ban` | Ban a member from the server (Ban Members). |
| **Mod** | `/kick` | Kick a member (Kick Members). |
| **Mod** | `/mute`, `/timeout` | Apply a Discord timeout (Moderate Members). |
| **Auto-Mod** | `/block-word`, `/unblock-word` | Manage blocked words (Manage Server). |
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
