# MobaIcons

MobaXterm session 行的类型图标，来源：[Tabler Icons](https://github.com/tabler/tabler-icons)
（MIT License，Copyright (c) 2020-2026 Paweł Kuna）。

取源提交：`a4ce1404bc6d24d3c365afe7b258d6bf6f48d62d`

## 文件构成

| 文件 | 对应 | 源图标（`icons/`） |
| --- | --- | --- |
| `ssh.png` | SSH 会话 | `outline/server.svg` |
| `wsl.png` | WSL 会话 | `outline/terminal-2.svg` |
| `session.png` | 其它协议（RDP / VNC / SFTP…） | `outline/terminal-2.svg` |
| `folder.png` | 目录行 | `filled/folder.svg` |

## 是怎么生成的

Tabler 的 SVG 是**单色描边**图（`stroke="currentColor"`，只有 24×24 的路径数据）。
CmdPal 的 `IconHelpers` 只接受位图（PNG），没有从流构造图标的公开 API，所以这里把 SVG
**栅格化成 PNG**，并在栅格化时把 `currentColor` 替换成每类一个固定颜色，使其在明暗主题下
都清晰：

| 类型 | 颜色 |
| --- | --- |
| SSH | `#4C8DF6`（蓝） |
| WSL | `#E95420`（橙） |
| 其它 | `#8A93A5`（灰） |
| 目录 | `#E8A33D`（琥珀） |

复现步骤（需 `rsvg-convert`、`optipng`、`zopflipng`）：

```sh
sed 's/currentColor/#4C8DF6/g' server.svg > _c.svg
rsvg-convert -w 64 -h 64 -o ssh.png _c.svg
optipng -o7 -strip all ssh.png
zopflipng -y -m ssh.png ssh.png
```

> 与 `Assets/MaterialIcons/` 一样，压缩这步不能省，否则体积会白涨。
> 换源图标时同步更新上表的文件名与提交号。
