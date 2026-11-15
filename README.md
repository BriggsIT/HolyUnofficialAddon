# Holy Unofficial Addon

An unofficial addon for **Mad Games Tycoon 2**, made to sit alongside **The Holy Expansion**. It makes the whole game
livelier, from a 1976 start to the late game:
- **Industry:** events, the other studios you deal with, and historical landmark games to make before they do.
- **Designing games:** a third and fourth topic, eleven more languages, and a design screen that shows the market and
  the whole project.
- **Running your company:** MMOs you can keep alive for decades, your own digital store, sales, a yearly
  convention and an awards ceremony.
- **Tools:** an overview of all your rooms, current charts, and contracts with outside studios.
- **Quality of life:** faster speeds, press reviews and many small fixes.

Where it overlaps with The Holy Expansion, the two are merged into one system rather than stacked.

It's a hobby project for friends: not for sale, and not affiliated with Eggcode Games or with The Holy Expansion's
author.

**Version:** 0.32.0 ([what's new in each version](CHANGELOG.md)) · **Game:** Mad Games Tycoon 2 (Steam, PC) · **Loader:** BepInEx 5

<img src="Screenshots/overview/bottom-bar.png" width="100%" alt="The bottom bar with the addon's buttons">

<p>
<img src="Screenshots/industry/studios.png" width="32%" alt="The Industry window">
<img src="Screenshots/mmo/dashboard.png" width="32%" alt="An MMO's dashboard">
<img src="Screenshots/store/home.png" width="32%" alt="Your digital store">
</p>

---

## Contents

- [Installing](#installing)
- [Features](#features)
  - [Bottom bar and menus](#bottom-bar-and-menus)
  - [Industry events](#industry-events)
  - [Landmark games](#landmark-games)
  - [Designing games](#designing-games)
  - [Rooms Overview](#rooms-overview)
  - [MMO Management](#mmo-management)
  - [Your Digital Store](#your-digital-store-from-2003)
  - [Sales Events](#sales-events)
  - [Yearly convention](#yearly-convention)
  - [MGH Games Awards](#mgh-games-awards)
  - [Current Charts](#current-charts)
  - [Contracts](#contracts)
  - [More features](#more-features)
- [Working with The Holy Expansion](#working-with-the-holy-expansion)
- [Settings](#settings)
- [Compatibility and saves](#compatibility-and-saves)
- [Known issues](#known-issues)
- [Reporting a bug](#reporting-a-bug)
- [Building from source](#building-from-source)
- [Repository layout](#repository-layout)
- [Credits](#credits)
- [Changelog: every version](CHANGELOG.md)

---

## Installing

**You need:** Mad Games Tycoon 2 with **BepInEx 5** (tested with 5.4.23). If you already play with The Holy
Expansion, you have BepInEx already.

1. On this page, click **Code > Download ZIP** and unzip it.
2. Open the game folder: in Steam, right-click Mad Games Tycoon 2 > **Manage > Browse local files** (the folder with
   `Mad Games Tycoon 2.exe` in it).
3. Copy the **`BepInEx`** folder from the zip's **`Install`** folder into the game folder, and say yes to merging
   folders. The addon ends up in `BepInEx\plugins\HolyUnofficialAddon`.
4. Start the game. The addon's buttons appear on the bottom bar.

**Updating:** do the same again and let the new files replace the old ones. Your settings and saves are kept.

**Removing:** delete `BepInEx\plugins\HolyUnofficialAddon` (and, if you like, its settings file
`BepInEx\config\holyunofficialaddon.mgt2.cfg`). Back up your saves first: loading a save without the addon hasn't
been tested.

---

## Features

### Bottom bar and menus

Six buttons next to the game's own:
- the addon's window;
- **Contracts**;
- **Industry**;
- **Rooms Overview**;
- **MMO Management**;
- **Your Digital Store**.

The addon also adds entries to the game's menus:
- **Management:** Your Copy Protection, Your Digital Store.
- **Statistics:** Current Charts.
- **Development room:** Develop Copy Protection, MMO Management, Updates & Addons.

Below 1080p, an arrow button before them shows or hides them, and where they'd cover the game's speed buttons they
sit in a row just above the bar. Windows too big for the screen are scaled down to fit above the bar. At 1080p and
up, everything looks as it always did.

A button or entry for something you can't use yet is greyed out, and its tooltip says why, like the game's own.

<p>
<img src="Screenshots/overview/management-menu.png" width="180" alt="The Management menu">
<img src="Screenshots/overview/statistics-menu.png" width="180" alt="The Statistics menu">
<img src="Screenshots/overview/development-menu.png" width="230" alt="The development room menu">
</p>

### Industry events

<p>
<img src="Screenshots/industry/studios.png" width="49%" alt="The Industry window: studios and your standing">
<img src="Screenshots/industry/studio-page.png" width="49%" alt="A studio's page">
</p>

The industry around you moves, and things land on your desk.

- **Headlines** in the news every few weeks change the market. They include:
  - a genre craze, which makes it the trend in the top bar, or fatigue, which puts it out of fashion;
  - another studio's hit or flop;
  - a console price cut, a console selling out at launch, a console war;
  - studios delaying games, showing off their next one, opening and closing;
  - a star changing studios, a new games magazine, the Christmas must-have machine, budget re-releases and a new
    chain of games shops.
- **Decisions** pop up at random, more often when the moment fits (you just released a game). They average every
  2-3 months, and you can set them to Calm, Normal or Busy. Each one has 2-4 choices that change your money and
  fans, the market, and your standing with other studios.
  - **Any year:**
    - A Cassette in the Post;
    - Print Your Code?;
    - Cabinet Deal;
    - A New Machine Wants Games;
    - Share a Stand?;
    - A Copy of Your Game;
    - Publishers Bid (for your game in development: the winner publishes it, like an exclusive contract);
    - A Studio Needs Help (to finish its announced game).
  - **The garage and arcade years (1976-1990):**
    - Computer Club Night;
    - A Studio Asks for a Hand;
    - Talked Down in Print;
    - First Look;
    - A Guild for Game Makers;
    - A Distributor in Trouble;
    - a Pocket Version of your hit;
    - A High-Score Contest.

    Most need no released game, so a 1976 start is busy from the first months.
  - **The console and cartridge years (from 1985; some run to the early 2000s):**
    - A Cartridge Shortage;
    - The Game of the Film;
    - A Cartoon of your hit;
    - A Harsh Review;
    - A Star Wants to Join You;
    - Rent It Out?;
    - Stuck on your game (guide, hint line or free tips);
    - An Exclusive Offer (a timed exclusive);
    - Concerned Parents;
    - Swap Know-How?;
    - Shelf Space.
  - **The 3D and online years (from the mid-1990s; some end in the 2000s):**
    - Engine Request (license an engine you don't sell to one studio, or put it on sale);
    - The Mod Team (hire, back or buy a hit mod of your game; with Holy, its "Mod of the month");
    - A Star Leaves (one of your best people founds a real new studio);
    - Closure Auction (bid for a closed studio's series);
    - Cover Disc;
    - Graphics Card Bundle;
    - Fund a Port (a platform maker pays toward porting your game);
    - CD Burners (every copy protection weakens);
    - Hidden Content.

    Headlines for these years include the dot-com boom and bust, internet cafés, the used-games market and the
    handheld revival. Rarely, a struggling console maker quits hardware (a pop-up: hire its engineers or port
    your games), and its studio goes third-party.
- **Community Work** (with The Holy Expansion's Communities): a toggle in a Support room's menu makes the room start
  Holy's modding tools and tournaments for your games by itself, pausing calls while it works.
- **Standing with every studio**, from Rival through Neutral and Friendly to Ally, grows out of what happens between
  you. Good turns also improve the game's own relation with them, so you get better publishing terms.
- **Rivals:**
  - The rival you get on worst with is your **arch-rival**. Every few months it moves against you: poaching your
    best people, copying your hit, clashing with your release, or trash-talking you.
  - Once you're established, a studio your size may declare you its rival.
  - On any rival's page you can poach a developer, spread rumours, make peace, or at 100% rivalry drive them out of
    business.
- **Competitors:** about four studios that meet you most in the market right now: games in your genres, about your
  size, something out or coming. They're picked again every year and after each of your releases. Competing isn't
  hating (an ally can be a competitor), but they fight you for players:
  - **Release Clash:** their game in your genre lands with yours. With a release date set, their game really moves
    onto your week, and the better-reviewed one wins the launch weeks. Delay yours, hold the date, or out-market it.
  - **Talent Race:** they offer one of your good people a job. Match it, or let them go.
  - **Price War:** they cut the price of the game fighting yours (games you publish yourself). Match it, or hold
    your price.
  - **Same Idea:** they announce a game in the same genre as yours in development. Rush it, make it stand out, or
    ignore it.

  At most one fight every couple of months. Fights that go badly cost standing, so a competitor you keep clashing
  with can turn into a rival. Shelf Space, A Copy of Your Game and the hit headlines also pick competitors first.
- **The Industry window** has three tabs:
  - **Studios:** each studio's stars, favourite genre, game in development, your standing and your last dealing.
    Arch-rival and competitors are tagged, with filters for both. Click one for its page: what happened between
    you, why they compete with you, and its games.
  - **Recent News:** the latest headlines and decisions. With cheats on (Game settings), a Test Events button fires
    any event on demand.
  - **Your Deals:** revenue shares, fees and promises.

<p>
<img src="Screenshots/industry/news.png" width="49%" alt="Recent News">
<img src="Screenshots/industry/deals.png" width="49%" alt="Your Deals">
</p>

<p>
<img src="Screenshots/industry/arch-rival.png" width="49%" alt="Your arch-rival's page: the rivalry, your record and your moves">
<img src="Screenshots/industry/competitor-page.png" width="49%" alt="A competitor's page, with why they compete with you">
</p>

<p>
<img src="Screenshots/industry/rivals.png" width="49%" alt="The Rivals filter, with your arch-rival tagged">
<img src="Screenshots/industry/competitors.png" width="49%" alt="The Competitors filter">
</p>

### Landmark games

Historical games you can make first, before the studio that made them for real: 22 so far, from Breakout (1976) to
SimCity and Populous (1989), on The Holy Expansion's real franchise list.
- **Claiming one:** when a new game of yours is finished and fits a landmark (its genre and topics, a good enough
  projected review, and the years), a pop-up offers to make it that landmark. It's renamed, shows the landmark's
  screenshots, and its studio never makes it. A landmark can be made from 3 years before its real year until its
  studio releases it.
- **At release:** IP points, fans of its genre, hype, and its genre becomes the trend. A second landmark in another
  genre offers to make that your special genre.
- **Studios make them for real**, on time and in the right genre, if you don't.
- **Series follow history:** later games of a series in Holy's list (Mega Man X, Metal Gear Solid...) are made as
  sequels by whoever owns the series, not as new series. Yours are never made by a studio.
- **Landmarks tab** in the Industry window: the landmarks made so far, and who made them. "Show upcoming landmarks"
  in Game settings lists the rest, with what it takes to make each.
- **Pictures:** each landmark shows real screenshots from `BepInEx\plugins\HolyUnofficialAddon\landmarks\pictures\<landmark>`
  (PNG or JPG, any size, cropped to 4:3). Without any, it shows the game's usual screenshot.

### Designing games

- **Third and fourth topics:** two research items, **Layered Settings** (from 1988) and **Rich Worlds** (from 1996),
  open Third topic and Fourth topic buttons under Main topic and Subtopic. A topic that suits the genre and goes with
  the others lifts the review (up to about +5%); one that doesn't lowers it. Each adds about 10% to the development
  cost and time.
- **Eleven more languages:** Portuguese, Dutch, Greek, Hungarian, Czech, Turkish, Romanian, Ukrainian, Arabic, Hindi
  and Indonesian, each from its year, in a second row of flags. Each adds 1-2% to sales and costs the game's
  translation price. Released games can get them with **Translate the Game** in Updates & Addons.
- **The design screen** grows a row to fit them. The platform page chooses the timed exclusive, the design focus page
  has a **Market Check** (the trend, your genre's popularity, similar games other studios are making), and the
  financing page a **Project Overview** (cost, work and roughly how long, topic mix, languages, landmark, timed
  exclusive).

### Rooms Overview

<img src="Screenshots/rooms/rooms-overview.png" width="80%" alt="Rooms Overview">

Every room at a glance:
- its job and progress, including Holy's and the addon's jobs;
- what it does by itself (repeat, auto research, autopilot...);
- who supports whom, including Holy's studio links;
- what needs a look:
  - idle, and why (no calls, waiting for money, no bugs left...);
  - staffing problems;
  - jobs at risk, such as a contract that won't make its deadline.

Click a room to go to it: the camera moves there and its menu opens. It opens from the bottom bar, or from a button
in every room's menu.

### MMO Management

<p>
<img src="Screenshots/mmo/dashboard.png" width="68%" alt="An MMO's dashboard">
<img src="Screenshots/mmo/game-cards.png" width="25%" alt="The MMO tab and an MMO's game card">
</p>

Keep MMOs alive for decades, like the long-running ones in real life. **MMO Management** is in the development room
menu and on the bottom bar. It lists your MMOs, and each gets a dashboard with:
- subscribers, income and interest;
- its place in the subscription charts;
- bots;
- how its graphics and tech hold up today;
- two years of subscriber history.

| Project | What it does |
|---|---|
| Graphics Overhaul | Brings its graphics back to the standing they had at launch, against today's games. |
| 64-bit Client | From 2005; lifts the 2007 graphics ceiling of 32-bit clients. |
| Engine Upgrade or Migration | Newer engine features, or a move to another engine. |
| World Expansion | Grows the game a size, with a wait before the next step. |
| Content Patch | Keeps players busy for a while (not right after an expansion pack). |
| New Platforms | More platforms, past the game's usual limit of four. Expansion packs come to all of them. |
| New Game Systems | Add gameplay features after release. |
| Age Rating Change | A wider audience, though some players leave. |
| Bot Ban Wave | Clears out bots; income dips, real players stay longer. |
| Stability Patch | Fixes crashes and bugs. Also catches the leftover bug fractions the game couldn't fix. |
| Welcome-Back Campaign | Wins back former players. |
| Free Trial | More new players, but bots love free accounts. |
| Classic Servers | The MMO as it was at launch, in phases, as realms or its own game. |
| Expansion Pack / Free Update | The game's own, now in one place. |

Also:
- **Bots and anti-bot systems:** bots sit among your subscribers and drive real players away. Build your own
  anti-bot system for a genre, license it to other studios, or license a rival studio's, and keep it updated.
- **Autopilot:** runs the routine projects for an MMO within rules you set, such as money to keep and rooms to leave
  free.
- **Subscription Charts:** every MMO on the market by subscribers, with its bots.
- **Modern or old-school:** modern MMOs that look dated lose players. Old-school ones keep a smaller, more loyal
  audience.
- **IP:** your MMOs keep their IP alive while they still have players.
- **Game cards:** your MMO's chart place, subscribers and bots on its game card, plus a yearly **Best Ongoing Game**
  award.
- **MMO tab:** in the game list on the right, next to the game's own tabs, it shows only your MMOs and their
  expansion packs.

<p>
<img src="Screenshots/mmo/list.png" width="49%" alt="Your MMOs">
<img src="Screenshots/mmo/anti-bot.png" width="49%" alt="Anti-Bot Systems">
</p>
<p>
<img src="Screenshots/mmo/anti-bot-new.png" width="49%" alt="Developing a new anti-bot system">
<img src="Screenshots/mmo/autopilot.png" width="49%" alt="MMO Autopilot">
</p>
<img src="Screenshots/mmo/charts.png" width="49%" alt="MMO Subscription Charts">

### Your Digital Store (from 2003)

<p>
<img src="Screenshots/store/home.png" width="66%" alt="Your digital store">
<img src="Screenshots/store/in-development.png" width="30%" alt="The store client in development">
</p>

Build your own digital PC store in a development room, with one of 12 logos only your store can use. Other studios
sell through it and pay you a cut. It needs servers, and it grows with features, its catalogue, its reputation and
studios' trust. Its windows take your store logo's colour.

- **Studios:**
  - Studios ask to sell on your store and pay the signing fee you set. Big studios ask in a pop-up.
  - You can sign them, ask for more or decline.
- **Front page:**
  - Feature your own games.
  - Or sell the spots to studios by the month: they pay by the week, and their game sells more through you.
- **Features:** user reviews, friends and chat, automatic updates, regional pricing, achievements and cloud saves,
  each built in a development room.
- **Wishlists and early access** for your games in development.
  - With The Holy Expansion's Launch Planner on (its default), there's one launch system: your store adds its
    users' wishlists to Holy's, early access is Holy's (started from the store's Wishlists page or a Marketing
    room), and there's one launch boost.
  - Launches with wishlists make the news, with the boost they give.
- **Your MMOs in the store's launcher.**
- **Storefronts on consoles you make.**
- **Charts:** your store's best sellers, and how you rank against the rival stores.
- **Rival stores** open, change their fees and close on a real-life-like timeline from 2003. They take their cut
  of your games sold through them (10-30%); you can switch that off.

<p>
<img src="Screenshots/store/studio-request.png" width="49%" alt="A studio asking to sell on your store">
<img src="Screenshots/store/studios.png" width="49%" alt="The studios on your store">
</p>
<p>
<img src="Screenshots/store/front-page.png" width="49%" alt="The front page, with spots sold to studios">
<img src="Screenshots/store/features.png" width="49%" alt="Store features">
</p>
<p>
<img src="Screenshots/store/top-sellers.png" width="49%" alt="Your store's best sellers">
<img src="Screenshots/store/rival-stores.png" width="49%" alt="Rival stores">
</p>

### Sales Events

<p>
<img src="Screenshots/sales/sale-popup.png" width="49%" alt="The pop-up as the Summer Sale starts">
<img src="Screenshots/sales/sales-events.png" width="49%" alt="Planning the year's sales">
</p>

One system for seasonal sales. It replaces The Holy Expansion's Seasonal Sales and uses its numbers.

- **Summer and Winter Sales** run in every big store; your store adds a **Spring** and an **Autumn Sale**.
- **A pop-up as each sale starts:**
  - your games' discount (25/50/75%), with what it does to copies and money;
  - your store's own sale for other studios' games.
- **Sale fatigue:** join too many sales in a year and players start waiting for the next one.
- **Plan ahead:** set your plans for the year on the Sales Events page, and let them run without asking if you
  like.

### Yearly convention

<img src="Screenshots/convention/planner.png" width="62%" alt="Planning the yearly convention">

Your own convention every August, for all your games. It replaces The Holy Expansion's Fan Convention and the old
MMO FanFest.

- **Planning:** plan it from a Support room, or from the pop-up in early August.
  - **Venue:** livestream, fan meetup, convention or mega event.
  - **On stage:** games in development (premieres), MMOs with an announcement, consoles, and released games to
    showcase or tease a sequel.
  - **Extras:** merch, developer panels, gifts for lapsed players, and booths other studios pay for.
- **Preparing:** a Support room prepares it, and the usual work rules and supporting rooms count. Or hire an event
  agency.
- **The day:** how ready it is when the doors open decides how well it goes. A results screen follows.

### MGH Games Awards

<img src="Screenshots/awards/nominees.png" width="62%" alt="The awards ceremony">

The December awards become a ceremony:
- **12 awards**, with up to five nominees each;
- an envelope for every winner;
- a **Worst Game of the Year**.

The game's own categories keep their prizes. One ceremony never pays out more than sweeping the game's own awards
could.

### Current Charts

<p>
<img src="Screenshots/charts/top-sellers.png" width="49%" alt="Current Charts: top sellers">
<img src="Screenshots/charts/coming-soon.png" width="49%" alt="Current Charts: coming soon">
</p>

**Statistics > Current Charts** shows what sells right now:
- **top sellers** this week;
- **genres**, with what's trending and what's out of fashion, and each genre's best sellers;
- **consoles**, handhelds, PCs, mobiles and arcades by sales this week;
- **studios** by money made this week;
- **new releases**;
- **coming soon:** the most anticipated games with their hype and how far along they are, plus all of your games
  in development with their wishlists.

<p>
<img src="Screenshots/charts/genres.png" width="49%" alt="Current Charts: genres">
<img src="Screenshots/charts/consoles.png" width="49%" alt="Current Charts: consoles">
</p>

### Contracts

<p>
<img src="Screenshots/contracts/research.png" width="49%" alt="Contracts: research">
<img src="Screenshots/contracts/game-work.png" width="49%" alt="Contracts: work on a game in development">
</p>

Hire outside studios instead of doing everything in-house:

- **Research:** bundle any number of topics, genres, features, hardware and other research into one contract.
  Studios quote a price and a time.
- **Game work** on a game in development:
  - graphics, sound, gameplay and tech passes;
  - bug fixing;
  - extra hands to finish sooner;
  - QA and studio work.
- **Active Contracts:** running jobs, and what recently finished.
- Running contracts show as small cards on screen that you can drag where you like.

<img src="Screenshots/contracts/active.png" width="49%" alt="Active contracts">

### More features

- **Timed exclusives:** on the new-game screen's platform page, launch a multiplatform game on one platform first.
  The others follow 3 to 24 months later, and the platform maker pays a deal.
- **Your own copy protection:** research it (from 1980) and develop it in a development room, from disk checks to
  always-online DRM. Keep it updated, and license it to other studios.
- **More kinds of updates** in Updates & Addons for your released games:
  - **Port to More Platforms**, of the same kind;
  - **Update the Engine**;
  - **Add Gameplay Features**;
  - **Change the Age Rating**;
  - **Translate the Game**, into the new languages.

  Addons are no longer blocked by features added after release.
- **Press reviews:**
  - your games are reviewed by 31 named parody outlets, each with its own logo, scoring scale, reviewers and
    genre-aware text;
  - a strip shows every score;
  - English game language only.
- **The addon's window:**
  - change the new-game settings mid-game;
  - switch any of the addon's features on or off;
  - an employee editor, behind **Enable cheats** in Game settings.
- **Faster speeds:** **Turbo** (6x) and **Ultra** (12x) buttons, also on keys **4** and **5**.
- **Building:**
  - looser furniture placement, with a collision leniency slider;
  - furniture overlapping furniture;
  - any object in any room;
  - placed furniture no longer gets picked up by itself after loading a save.
- **Company logos:** 32 extra logos with matching names, only for you.
- **Performance:** a much faster new-game screen in big saves, lighter versions of some of the game's busiest code,
  and a stutter finder that writes a report of what makes the game stutter (off by default).
- **Finances:** the addon's money shows under the right categories on the finance screen (Licenses, Server costs,
  Merchandise...).

<p>
<img src="Screenshots/more/timed-exclusive.png" width="40%" alt="Choosing a timed exclusive">
<img src="Screenshots/more/copy-protection.png" width="57%" alt="Your copy protections">
</p>
<p>
<img src="Screenshots/more/copy-protection-new.png" width="49%" alt="Developing a new copy protection">
<img src="Screenshots/more/review-gameproz.png" width="24%" alt="A press review">
<img src="Screenshots/more/review-pcgamr.png" width="24%" alt="Another press review">
</p>
<p>
<img src="Screenshots/overview/addon-window.png" width="32%" alt="The addon's window">
<img src="Screenshots/overview/game-settings.png" width="29%" alt="Game settings">
<img src="Screenshots/overview/employee-editor.png" width="36%" alt="The employee editor (Enable cheats)">
</p>

---

## Working with The Holy Expansion

The addon is built on The Holy Expansion 1.8.3. Where both do the same thing, the deeper version wins and the other
is switched off, keeping its good parts:

- **Sales Events** replace Holy's Seasonal Sales; Holy's menu entry opens them.
- **The yearly convention** replaces Holy's Fan Convention. Holy's Support-room button opens its planner, and Holy's
  own room task prepares it.
- **Rivals** come from your standing with studios, in place of Holy's Rival Studio. Holy's moves and yours are
  kept, and its rival carries over; its menu entry opens your arch-rival's page.
- **Holy's Launch Planner** is the one launch system: your store feeds its wishlists, early access is Holy's, and
  there's one launch boost. Launches with wishlists make the news.
- **Holy's live-service seasons** started automatically now need a free Marketing room to prepare them, like ones
  you start yourself.
- **Shared sales bonus limit:** the addon's sales bonuses count toward Holy's limit instead of stacking on top.
- **Holy's piracy:** updates from this addon cut pirated copies, like a normal update.
- **Holy's price curve:** the addon's costs and income follow it (the store, contracts, MMO projects, anti-bot
  systems, the convention, industry events), so they keep pace with Holy's over the years.
- **Supporting rooms:** they count in Holy's time estimates, and show the progress of Holy's jobs.
- **Holy's delayed platforms** can be changed during development.
- **Holy's "Your Anti-Cheat"** is greyed out until anti-cheat software exists.
- **Holy's cracked-console pop-ups** say why a move isn't possible, instead of "You can't afford that."
- **Holy's Movie & TV deals:** the industry event "A Cartoon" makes Holy's own animated-series deal for your series,
  premiered and judged by Holy, so the two never double up.

Most merges can be switched off in Game settings, which brings back Holy's own version. The Launch Planner merge
follows Holy's own "Launch planner" setting.

---

## Settings

Almost everything can be switched on or off in the addon's window under **Game settings**. All settings are also in
`BepInEx\config\holyunofficialaddon.mgt2.cfg`, which BepInEx creates the first time you start the game. That file
also holds a few extras, such as:
- an optional hotkey for the addon's window;
- how many outlets review each game;
- the Turbo and Ultra keys.

Settings marked "this game" in the window are saved with your save file. The addon's own options apply to all
saves.

---

## Compatibility and saves

- Made for and played with **The Holy Expansion 1.8.3** and **GameDev Assistant** installed. It should load without
  Holy (Holy's extras then simply do nothing), but that hasn't been tested.
- Made for **single player**. Multiplayer hasn't been tested.
- What the addon adds is saved inside your normal save file, so there's nothing extra to back up or move. Before a
  save is overwritten, the addon keeps a backup copy, and puts it back if saving fails.
- Saves from older versions of the addon load in newer ones.

---

## Known issues

- **New features need play-testing:** landmark games, series following history, the third and fourth topics, the
  new languages, industry events, the Launch Planner merge, the MMO tab, the Rooms Overview, the yearly convention and
  Sales Events are recent. Please report anything odd: numbers that feel off, or events that come too often or too
  rarely.
- **Other mods on the design screen:** the addon makes the new-game screen a row taller and lays out its new rows
  from what's on screen. A mod that changes that screen too could end up overlapped; the BepInEx log says what moved.
- **Room menu button:** the Rooms Overview button sits at the end of each room menu's tool row. Please say if it
  looks out of place.
- **Press reviews** are only written in English. With the game in another language, you get the game's own review
  screen.
- **Closing the game** can crash on the way out. That crash is in the game's runtime, and happened before the addon
  too.

---

## Reporting a bug

Open an [issue](https://github.com/BriggsIT/HolyUnofficialAddon/issues) and attach your log file:

1. Right after the bug, find the log: in Steam, right-click **Mad Games Tycoon 2**, choose
   **Manage → Browse local files**, and open the **BepInEx** folder. The file is **`LogOutput.log`**.
2. **Send it before you start the game again.** The log starts fresh every time the game starts, so the bug is
   only in it until then. Closing the game first is fine.

The one file has everything: the addon's own lines (marked `Holy Unofficial Addon`), and the errors from the game
and The Holy Expansion. Please also say what you were doing, the in-game year, and your screen resolution
(some layouts change below 1080p). A screenshot helps.

**Lag or stutters:** switch on **Stutter finder** in the addon's game settings, play until it happens, then send
**`StutterReport.txt`** from the same BepInEx folder as well.

---

## Building from source

The `Source` folder holds the code (C#, .NET Framework 4.7.2) and the art sources (logos, icons and banners drawn as
HTML/SVG and rendered to PNG). You need the [.NET SDK](https://dotnet.microsoft.com/download) and the game installed
with BepInEx, because the build references the game's own files.

```bash
cd Source
dotnet build -c Release -t:Install -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Mad Games Tycoon 2"
```

`-t:Install` copies the built addon straight into the game's `BepInEx\plugins` folder. Leave out `-t:Install` to
only build. Change `GameDir` to wherever your game is.

## Repository layout

| Folder | What's in it |
|---|---|
| `Install` | The ready-built addon, laid out like the game folder: copy its `BepInEx` folder into the game. |
| `Source` | The code and art sources. |
| `Screenshots` | The pictures in this page, one folder per feature. |

---

## Credits

- **Mad Games Tycoon 2** by Eggcode Games.
- **The Holy Expansion** by jay (EXTLC), which this addon builds on and adjusts in places with his blessing.
- **Aerin the Lion's** Unofficial MGT2 Performance Patch, for the idea behind the faster new-game screen.
- **BepInEx** and **HarmonyX**, which make mods like this possible.
- Logos, outlet names, store names and event names are original or parodies made for this addon.

See [CHANGELOG.md](CHANGELOG.md) for what changed in each version.
