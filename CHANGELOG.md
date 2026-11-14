# Changelog

## 0.23.3
- Industry window, Studios tab: the same text size on every row (studio names, what they make, your last note).
- Recent News: headlines saved before 0.23.1 no longer say "Sports Game games".
- Contracts: your money no longer sits on the banner's picture, and the Research tab's hint isn't cut off.
- Fixed: monthly store and MMO steps never ran (the month's last week was never recognised). Now:
  - studios buy empty front-page spots on your store;
  - studios that stop trusting your store can leave;
  - anti-bot licences you bought charge their monthly fee;
  - your own anti-bot system earns licence income from other studios;
  - heavy botting slowly lowers an MMO's user reviews.
- Current Charts, Consoles: rival consoles show their weekly sales (the game only keeps them for yours), so the
  list and the best-selling console are right. It fills in after a week.
- Store Studios page: "89 signed (79 with games out now)", so the two numbers no longer disagree.
- Store Charts: rival stores' descriptions are a readable size.
- New Copy Protection: every technology's description is the same, readable size.
- The rival stores page is now titled Rival Stores, like its button (it shared "Store Charts" with your top sellers).
- Sale pop-up: the cards' second line has room for two lines, so the Deep Sale's "less trust" isn't clipped.
- Contracts, Game Work: a studio's genre no longer runs into what the job brings ("+10 graphics · 3 weeks").

## 0.23.2
- **Genre crazes set the trend.** A craze makes its genre the game's trend genre (top bar, and the game's own trend
  bonus) for the rest of the current trend period, at least 12 weeks, like the game's trendsetter hits do. Genre
  fatigue makes its genre the trend's "out of fashion" genre. The trend's timing and topics stay the game's own.

## 0.23.1
- **MMO tab** in the game list on the right: only your MMOs and their expansion packs. It appears while you have one.
- Current Charts' Coming Soon always lists your own games in development: announced ones keep their real place
  even below the top 40, and unannounced ones come last, marked "not announced yet".
- The Holy Expansion's cracked-console and emulator pop-ups give the real reason when a move isn't possible (for
  example "They already work for you"), instead of "You can't afford that."
- Industry window: a studio's page no longer cuts off the publishing relation, and its notes are the same size.
- Industry news no longer says "Sports Game games" or "a Action game".

## 0.23.0
- **One launch system with The Holy Expansion's Launch Planner.** Before, a game on your store could get two launch
  boosts and earn early access twice. With Holy's Launch Planner on (its default):
  - Your store adds its users' wishlists for your PC games to Holy's wishlists, instead of keeping its own.
  - Early access is Holy's. The store's Wishlists page starts it in the fastest free Marketing room, or says why
    it can't yet. A game already in early access on your store carries on in Holy's.
  - There's one launch boost: Holy's, up to +120% fading over 4 weeks, now counting your store's wishlists.
- Launches with wishlists make the news (Holy only wrote them to its own log): how many wishlists, how many came
  from your store, and the launch boost.
- The store's Wishlists page shows Holy's numbers: wishlists, early-access players and income, and launch boosts.
- Current Charts' Coming Soon shows your games' wishlists.
- With Holy's Launch Planner off, or without Holy, the store keeps its own wishlists, early access and launch boost.

## 0.22.1
- Fixed: industry events treated studio stars as 0-5 when the game keeps them as 0-100. A rival's hit, a star changing
  studios or a staff swap could drop that studio to almost nothing, so its games came out weak and unhyped.
  Studios hit by this are put back to their stars in the game's studio data the first week after loading a save.
- The Industry window shows the right number of stars.
- Events about small studios (A Studio Asks for a Hand, Share a Stand?, A Studio Needs Help) can happen again, and
  publishers' advances in Publishers Bid are back to their intended size.

## 0.22.0
- **Industry events for the console and cartridge years (from 1985):** 16 new events. Some run on into the late 1990s
  and early 2000s.
  - Decisions:
    - A Cartridge Shortage, The Game of the Film, A Cartoon of your hit, A Harsh Review.
    - A Star Wants to Join You, Rent It Out?, Stuck on your game (guide, hint line or free tips).
    - An Exclusive Offer (it sets up a timed exclusive), Concerned Parents.
    - Swap Know-How? with a friendly studio, Shelf Space (a rival pays the shops).
  - Headlines: a console selling out at launch, a console war, a studio's big reveal, budget re-releases, a new chain
    of games shops.
  - More ways to win and lose standing: share chips with a studio that has none, warn a studio that its star wants
    to leave (or hire them), swap staff with friends, fight a rival for shelf space.
- Some effects now wait for a game's release: a film licence's sales boost starts when the game comes out.

## 0.21.0
- **Industry events for the garage and arcade years (1976-1990):** 12 new events, most of which need no released
  game, so a 1976 start is busy from the first months.
  - Decisions: Computer Club Night, A Studio Asks for a Hand, Talked Down in Print, First Look, A Guild for Game
    Makers, A Distributor in Trouble, A Pocket Version (an LCD handheld of your hit), A High-Score Contest.
  - Headlines: a rival's flop, a star changing studios, a new games magazine, the Christmas must-have machine.
  - More ways to win and lose standing with studios early on.
- With cheats on, the Industry window's Recent News tab has a **Test Events** page that fires any event on demand.

## 0.20.0 - 0.20.1
- **Industry events:**
  - News headlines that move the market: genre crazes and fatigue, hits, price cuts, delays, studios opening and
    closing.
  - Decisions that pop up at random, with choices: A Cassette in the Post, Print Your Code?, Cabinet Deal, A New
    Machine Wants Games, Share a Stand?, A Copy of Your Game, Publishers Bid, A Studio Needs Help.
  - Your standing with every studio (Rival, Neutral, Friendly, Ally).
  - A new **Industry** window on the bottom bar.
  - Pace (Calm, Normal, Busy) in Game settings.
- Tidier pop-ups: the industry decision, studio request and sale pop-ups fit their content, Publishers Bid shows
  both studios, and the Deep Sale card's text no longer gets cut off.
- Tidier lists: game names on the Industry window's Studios tab no longer run into the standing bar. In the Rooms
  Overview, permanent jobs (calls, merch) have no progress bar, each support link is listed once, and the text is the
  same size on every row.

## 0.19.0 - 0.19.1
- **Rooms Overview:** every room's job, progress, automations, support links and warnings (idle and why, staffing,
  jobs at risk). Click a room to go to it. It opens from the bottom bar and from a button in every room's menu.
- Fixed stutter near rooms supporting other rooms, and a slow Rooms Overview that jumped back to the top.

## 0.18.0 - 0.18.3
- **Yearly convention** for all your games, in place of The Holy Expansion's Fan Convention and the MMO FanFest:
  - venue, stage, extras and booths for other studios;
  - prepared in a Support room;
  - how ready it is on the day sets how well it goes.
- Any of your released games can go on stage: a showcase, or a sequel teaser.
- Locked options are greyed out with the reason as their tooltip: Holy's Your Anti-Cheat, Sales Events, MMO
  Management, Your Digital Store, and the rows in the addon's window.
- Menu entries, buttons and window titles use the game's Title Case.
- Holy's automatic live-service seasons now need a free Marketing room.
- The anti-bot autopilot rule only runs while the autopilot is on for an MMO, at most one update a week. Its setting
  is now on the Anti-Bot Systems page too.
- Time estimates count rooms that support the room, and supporting rooms show the progress of Holy's and the
  addon's jobs.

## 0.17.0 - 0.17.1
- **Sales Events** replace The Holy Expansion's Seasonal Sales:
  - Summer and Winter Sales, plus your store's Spring and Autumn Sales;
  - a pop-up as each starts;
  - your games' discount and your store's sale;
  - sale fatigue;
  - a planner.
- Fixed explanation text being cut off at the top of some store, anti-bot and MMO pages.

## 0.16.0 - 0.16.3
- New look for the MMO and store windows:
  - banners and stat cards;
  - icon buttons;
  - title bars in their own colour (the store's takes its logo's colour).
- **Current Charts** in the Statistics menu: top sellers, genres, consoles, studios, new releases, coming soon.
- **Store studios:** studios ask to sell on your store and pay a signing fee you set. Big ones ask in a pop-up.
- The Contracts window got the same new look.
- The employee editor is now behind **Enable cheats** in Game settings.
- The addon's money now shows under the right categories on the finance screen.
- Fixed:
  - placed furniture sometimes being picked up after loading a save;
  - "% done" showing a huge negative number for rivals' games.

## 0.15.0
- **Your Digital Store, part 2:**
  - sale events;
  - the front page and paid spots;
  - wishlists and early access;
  - your MMOs in the store's launcher;
  - storefronts on your consoles.

## 0.14.0 - 0.14.3
- **MMO Management, part 2:**
  - bots, and anti-bot systems you build or license;
  - bot ban waves;
  - stability patches;
  - welcome-back campaigns and free trials;
  - classic servers with phases;
  - the autopilot.
- Expansion packs come to every platform an MMO is on, and content patches wait two months after one.
- Fixed bugs that could never be fixed: under half a bug left over.
- Fixed a crash when switching the stutter finder off.

## 0.13.0 - 0.13.3
- **MGH Games Awards ceremony:**
  - 12 awards with up to five nominees each;
  - a Worst Game of the Year;
  - a new ceremony screen.
- Performance:
  - faster money text, so fewer garbage-collection pauses;
  - faster new games;
  - a fix for a once-a-second stutter from Holy's studio support.

## 0.12.0 - 0.12.2
- Faster new-game screen in big saves (idea from Aerin the Lion's performance patch).
- Stutter finder (off by default): writes a report of what makes the game stutter.
- Safer saving: the old save is kept, and put back if saving fails.

## 0.11.8
- Fixed: since 0.11.5, loading a save could cut some of your released games off their IP (IP showing as 0 or
  "none"). The IP points themselves were kept. Affected games are reconnected automatically when you load the save,
  and the log names them.

## 0.11.4 - 0.11.7
- MMO game cards: your MMO's place in the MMO subscription charts as a blue calendar in the card's top line.
- MMO game cards: the bar chart shows subscribers each week instead of sales.

## 0.11.3
- MMOs keep their IP alive while they have players, and MMO projects add IP points.
- New yearly "Best Ongoing Game" award at the December awards, for MMOs a year old or more.
- MMO expansion packs give the MMO a lasting boost to new players, a little more quality, and more IP points.

## 0.11.2
- MMO expansion packs and free updates moved into MMO Management. Released expansion packs no longer clutter the
  game cards.
- The MMO's chart place shows on its game card.

## 0.11.0 - 0.11.1
- Timed exclusives: release on one platform first, the others after 3 to 24 months, with a deal from the platform
  maker.

## 0.10.x
- Your digital store (from 2003), rival stores with real-life-like fees, store features, catalogue and charts.
- MMO Management and the store on the bottom bar, greyed out until available.
- Balance with The Holy Expansion: updates cut pirated copies, shared sales bonus limit, Holy's seasonal sales from
  2003.
- Holy's delayed platforms can be changed during development.
- Fixed platform cards flickering on the change-platform screen.

## 0.9.x
- MMO Management: dashboard, graphics overhauls, 64-bit clients, engine upgrades and migrations, world expansions,
  content patches, old-school or modern MMOs.
- MMO versions of new platforms (past the four-platform limit), new game systems and age rating changes, plus the
  MMO Subscription Charts.
- Fixed the game getting stuck in a menu after closing the MMO addon screen.

## 0.8.x
- Updates & Addons: port to more platforms, engine updates, new gameplay features and age rating changes after
  release.
- Addons are no longer blocked by features added after release.

## 0.7.0
- Renamed from "Editor" to Holy Unofficial Addon.
- 32 company logos that only you can use.

## 0.6.0
- Your own copy protection: research it, develop it, update it and license it to other studios.

## 0.4.0 - 0.5.x
- Contracts: hire outside studios for research or work on your games in development, with on-screen contract cards.

## 0.3.x
- Press reviews from named outlets with logos, reviewers and genre-aware text.

## 0.1.x - 0.2.x
- Employee editor, game settings mid-game, Turbo and Ultra speeds, looser furniture placement.
