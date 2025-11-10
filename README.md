# ODT-ItemInfo
Massive QOL Item Info mod for SPTarkov.
English only (for now).


# Features: 

## Rarity Recolor
This BETA feature clears and changes background color on EVERY item in the game based on MMO style rarity tier-list with colors that make actual sense. 
Tiers are based on trader level you can purchase or barter the item. 
Barters are considered +1 rarity level. 
Banned on flea market items are given highest rarity - overpowered. 
The tier list: 
* Common (grey background, bought with level 1 traders) 
* Rare (blue, level 2 trades for currency and level 1 barters) 
* Epic (purple, level 3 and level 2 barters) 
* Legendary (dim yellow, level 4 and level 3 barters) 
* Uber (bright yellow, level 4 barters only) 
* UNOBTAINIUM (bright green, super rare spawns, not 100% accurate to actual in-game spawns, especially for keys, but good enough) 
* OP (bright red, banned on flea market) 
* CUSTOM (dim red, not used by default). 

Works for 95% of items well enough to be very much usable. Can add tier name to Prices Info module. Add custom item rarities in config. 
![image](https://user-images.githubusercontent.com/33424002/215009820-772c0b0a-3659-4923-8b63-266ce0d05bd5.png)


## Mark Valueable Items
Marks most valuable items by adding symbols ★ and ☆ to item names and inventory icons based on item per-slot value (configured by thresholds in config) when sold to traders OR fleamarket AVG price.

## Bullet Stats In Name
Adds bullet stats to bullet name (damage / armor penetration). Calculates total damage for buckshot rounds. VERY usefull in raid, because bullet name is shown when check magazine action is used.
> 5.45x39mm PP gs (44/36)

# Description modules:

![image](https://user-images.githubusercontent.com/33424002/215010036-daafcc4c-760a-4750-91ad-372e3206ee97.png)


## Prices Info
Basic module that adds prices information to item description, includes avarage flea price and best trader to sell to.
> Flea price: 61703₽ | Ragman's valuation: 37386₽

## Barter Info
Adds information about how you can buy the item from traders, their levels, price or resources (barter resources short names are used and total sum is based on AVG flea prices)
> [T H I C C item case] Bought @ Therapist lv.4 < ★Defibrillator ×15 + ★LEDX ×15 + Ibuprofen ×15 + ★Toothpaste ×15 | Σ ≈ 12877545₽

## Production Info
If item can be crafted, adds information on resources and total crafting sum per item based on flea prices.
> [9x19mm AP 6.3] Crafted ×150 @ Workbench lv.2 < ☆Hawk ×2 + ☆Pst ×400 | Σ per item ≈ 1686₽

## Crafting Material Info
Shows if item is used in crafts along with other materials and profit delta based on flea prices only (this messes up calculation on some crafts that can be done insanely cheap using trader materials or items obtained from other crafts). This is a guideline for crafting profits, not a rule.
> [SSD drive] Part ×1 > Secure Flash drive ×3 @ Intelligence Center lv.2 < … + ★GPX ×1 + ☆GPhone ×1 | Δ ≈ 16234₽

## Barter Resource Info
Shows info if an item can be traded for something with traders along with other resources. Calculates total sum of all resourses (based on flea prices) and delta between buying the final item directly on flea or from trader. Positive delta = profit, negative = don't bother, buy it directly if you can.
> [Ibuprofen painkillers] Traded ×15 @ Therapist lv.4 > T H I C C item case < … + ★Defibrillator ×15 + ★LEDX ×15 + ★Toothpaste ×15 | Δ ≈ -9777545₽

## Quest Info
Adds information if the item needs to be handed in for a quest. Marks find in raid quest condition with a checkmark with an option to add this checkmark to an item name.
> [CMS surgical kit] Found (✔) ×2 > Ambulance @ Jaeger

## Hideout Info
Shows if item is needed for hideout construcion.
> [Secure Flash drive] Need ×3 > Intelligence Center lv.2

## Armor Info
Adds armor stats for armor level (usefull for Realism mod), effective durability calculation, material quality and per repair degradation.
> [BNTI Zhuk-6a body armor] Armor class: 6 | Effective durability: 94 (Max: 75 x Ceramic: 1.3) | Repair degradation: 17% - 22%

## Container Info
Adds slot effeciency calculation for rigs, backpacks and containers (number of internal slots / item size)
> [WARTECH TV-110 plate carrier rig] Slot effeciency: ×1.92 (23/12)

![image](https://user-images.githubusercontent.com/33424002/215010328-c6fe2241-6e35-4c2c-908f-c59c4b523d32.png)

## Headset Info
Adds headset actual audio stats with pseudo compression boost calculation. In theory, more compression  and lower ambient volume = better (BSG headset rarity tiering supports this theory), but it seems for me, in practice, it's not always the case in-game. Higher resonance means harsher sound and boost at filter frequency.
> [Peltor ComTac 2 headset] Ambient Volume: -5dB | Compressor: Gain 10dB × Treshold -25dB ≈ ×2.5 Boost | Resonance & Filter: 2.47@245Hz | Distortion: 28%

## Spawn Info
Shows pseudo spawn info based on BSG handbook data. Mildly useful, not representative to real spawns, can be safely disabled.
