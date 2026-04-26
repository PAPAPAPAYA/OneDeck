// devlog

//！ anything else
	// add space after annotations
	//// show card tag ---- deathrattle, linger
	//// cards tweaks
	// change rift to not be minion, need to have 1 rift to stage another rift
	//// check desc format
	//// check card desc and configuration inconsistentency
		//// output ready, need to check actual prefab configuration, refer to History
	//// chang blood-letting summoner to 有副作用的传送门, enhance friendly curse
	//// check unstable_portal behaviour
	//// change cardScript to use tab to indent

//! low priority
	// if we want to achieve synergy like "give power to next card" + "before power is given, stage self", we need to make the former a tag (like linger) to identify and react to
	// same as "bury next 3 cards" + "before burying cards, stage self"

//! refactor
	//// change all card prefab name to english
		//// new variable to store chinese name
	// ApplyStatusEffectCore()
		//// GiveSelfStatusEffect()
		//// ApplyPowerToCardInternal()
		//// TransferStatusEffects()
		//// these three didn't use ApplyStatusEffectCore(), might refactor later to clean up
	//// power reaction effect & when gain power, gain triple amount effect
		//// new SO to store game object
			//// can't do, SO can't store instance
			// well then store it in combat manager, might move it out to a component
		//// track last power receiver in value tracker manager
		//// new method to give status effect to objSO
		//// new SO to store last applied power amount
		//// new method to gain double last applied power amount
	//// make all chinese into english to avoid encoding errors
	//// curse card type id: make it a stringSO
//! design
	// decrease [deathrattle], [bury] should be more than [if buried]
	// sort cards with "generating resources" and "consuming resources"
	// update cards
	//// POWER_SIPHONER will one card destroy curse
		//// 1. copy half power from all friendly
		//// 2. transfer half power from all friendly
		//// 3. transfer 1 power from all friendly - go with this one
	//// multiple [stage friendly] will result in easy infinite, need figure out how to limit stage ability
		//// armed_summoner: stage 1 friendly; deal 3 dmg
			//// blood-letting summoner: stage 1 friendly; deal 2 dmg to self
		//// unstable_portal: stage 1 friendly; if buried, bury 2 hostile
			//// stage 1 friendly; bury 1 friendly
		//// overcharged_summoner: stage 1 friendly; enhance next 2
			//// consume 1 power: stage 1 friendly; enhance next 2
		//// curse_summoner: stage 1 friendly; enhance 1 hostile curse
			//// consume 1 hostile curse power: stage 1 friendly
		//// advance_portal: stage 2 friendly
			//// stage 2 friendly; gain 1 rest
		//// almighty: stage 1 friendly; bury 1 hostile; enhance 1 friendly; add 1 [rift]; enhance 1 hostile [curse]
			//// gain 1 rest

//! design choice
	// dmg multiple times don't trigger multiple on dmg received event, dmg multiple times will be more about utilizing power

//! feature
	//// skill to check infinite card combos
		// GiveStatusEffectToXFriendly, GiveStatusEffectToLastXCards, GivePowerToCardThatGotPower
	//// tool to validate no duplicate card type id
	//// skills and SOPs to generate test plan and execute test plan to test card prefabs
	//// script name check
	//// tell kimi to make a configuration checker
	//// rarity
		//// rarity attibute in card script
	// shop
		// iterate on shop based on rarity
	// fake shuffle
	
//! viscom
	// better power wisp
	// power gain card particle
	// motion design
		// show invoked cards not in reveal zone, compress rest of the deck
	// queue animation?
	

//! design change
	// should we change [bury 1 friendly] to [bury next 2]?
		// it's more powerful, maybe should be benefit, not cost
	//// should we change bury to only work on cards before start card?  no, there can be bury that only works on cards before start card

//! bug
	//// fixed copycurse (to me --> to them)
	// power wisp destroy timing
	//// some effects don't include card in reveal zone
	// test progress:
		// bury: ai tested
			//// MARTYR (DEATHRATTLE 批次) — GiveAllFriendlyStatusEffect 不读取 includeSelf 字段，Martyr 自身总会获得 Power。
			// GRAVE_PORTAL — 位于 top 的 portal 被 StageMyCards 排除时，预期行为与实际行为存在差异。
			// GRUDGE (A-1, A-5, A-6) — CombatUXManager.me 为 null 导致 StatusEffectGiverEffect.GiveStatusEffectToLastXCards() 回调未执行，Power 未被施加 → 通过创建 CombatUXManager 实例修复。
			// LARGE_SCALE_DEATH (A-1) — BuryLastXCards(4) 因 deck 索引计算问题只 bury 了 3 张 → 调整 setup 顺序后修复。
			// GRAVE_TOGETHER (A-1) — owner 侧 bury 计数为 0，因 friendlyInGraveAmountRef 在 bury 前未正确更新 → 确保 deck 状态正确后修复。
			// SLIME (A-3) — CombatFuncs.me / physical card prefab 为 null 导致 AddSelfToMe 路径异常 → 通过隔离 burial 调用路径修复。
	// test strategies:
		// strategy A: simulate logic in editor mode
		// strategy B: simulate logic in play mode
		// strategy C: regression batch test (different dimension, check if one type of configuration is correct)
	//// spike skeleton game event listener duplicates
	//// slime lacks consumming counter
	//// generate test plans
		//// bury
			//// deathrattle
		//// general
		//// conjure
		//// curse
	// curse effect
		// when curse is added, power wisp target pos is weird cause curse is not yet in position
	//// bury animation bugged, suspect stage also has same problem





//// oldtodo
// bug
	//// start card movement
	//// attack animation need to check effect, not card owner
	//// block input when shuffling
	
// visual communication
	//// attack animation
	// status effect
		//// infected WIP, need to change card material as well
			// need to do the spit a poison thing to card thing
		// power
	//// stage and bury
	//// minion
// game event:
	//// when a friendly minion is added
	// if next X cards contain friendly/hostile cards
	// if card is moved (staged, buried, delayed, forwarded)
	// X consecutive friendly/hostile
	// is moved
	// friendly minion revealed
	//// friendly fly exiled
// condition:
	// if next X cards contain friendly/hostile cards
// feature:
	//// minion / actual card
	//// new way of adding fatigue
// status effect:
	// nutrious: when revealed, heal you for 1
	// escape goat: bury target becomes this card
// effect:
	//// delay bugged: try bury
	// forward
	// add a copy
	// deal dmg = next X cards friendly/hostile amount
	//// deal dmg = card amount in friendly deck
	//// new exile: destroy card
	//// exile friendly/hostile minion
	// skip
	// heart change specific card
	

//! quirks
	//! deck and grave effects don't show fail message, would be too much info? or too few surprises?
	//! status effect damage counts as status effect owner card's damage
	//! simplified hp event to [when session owner takes dmg] and [when enemy takes dmg]
	//! when adding card to / back to combined deck, add them to the end of deck; only shuffle when round finished
	//! currently we only use round num to match decks
	//! chains are closed when: [1] same card, different effect is trying to get invoked; [2] waiting input confirm;
	//! chains are composed with a string of effect containers
	//! don't put multiple effect instances with loopable effect in one card, will stack overflow; put multiple loopable effects in same effect instance
	//! no beforeIDealDmg time point and game event, would stack overflow; HPAlterEffect calculates dmg before dealing dmg

// UX Prototype
	//// DOTween for the real shit
	//// or consider Unitask
	// combat
		//// show deck
		//// show grave
		//// put grave card back to deck
		//// motion queue for physical cards to process sequence of motion
		//// use lerp in update in card phys obj script instead of coroutine
		//// accounts for effects that change deck
			//// need to fix [when undead stab is the last card]
			//// add new card
			//// grave to deck
			//// deck to grave
			//// stage & bury
			//// may need to move logic of moving revealed card to grave later in the process, do it when next card is revealed
				//// need to fix revealed card info's #
				//// need to clean up revealed card info when need to put last card to grave
		//// delete physical cards when exiting combat
	// shop
		//// show deck
		//// show shop
		//// buy card
		//// sell card
	// result
		//// tap to continue



#region Refactorings
// refactoring
	//// no grave
	//// revealed card is put back to the bottom of deck
	//// start card effect: shuffle deck (including start card)
	//// start card marks the beginning of a new turn
	//// base dmg amount SO, so that i can balance it quicker
	//// use card's game object's name instead of cardscript's cardName
		//// take out "(Clone)"
	//// change tag to status effect
	//// clean up effect name, effect description and use effect game object's name
	//// clean up effect script's variables?
		//// refactor how infection deal dmg
			//// not removing infected tag after resolved
				//// new effect: remove tag
			//// get rid of the card script in tag resolver
		//// test stab
		//// test infection
		//// test change of heart
	//// yup we tripped ourselves by not discerning between dealing dmg and healing, separate these two
	//// refactored game event so RaiseSpecific() also raise the game object's children's game event listeners
	//// refactored tag resolver so it works with specific game event raise
	//// encapsulate moving cards around and other supplement functions to another script and add it as a required component to combat manager
	//// fucking hell you are stupid, refactor to call specific events and any events so that cost only check for cost, and invoke card effects through game event SO
	//// give tags to random cards and tag related refs are stuffed into the parent script ---- effect script
	//// effects can use a parent class to initialize some context
	//// button prompt before shuffling
	//// make text codes in effect scripts StringSO
	//// refactor tags to use game event SOs
#endregion

//// deck tester
	//// record session amount
	//// auto space until session amount reached
	//// print win rate
	//// average finish hp
	// dmg stat
		//// ave. dmg output per session
		// ave. dmg output per session per card

#region abandoned
// abandoned: too much work, not economic
// card maker
	//// make a new prefab
		//// basic info
	//// add effect prefabs to test hooking up unity event
	// make effect object
	// setup CostNEffectContainer events
		// cost events
		// effect events
	//// setup game event listener events
#endregion

// card structure
	//// same structure from slash/ but expanded to support multiple costs and effects
		//// card structure
			//// cost check
			//// effect
			//// assign effect target ref


// battle: text demo
	//// when a new round begins and the first card is enemy's, it will be sent to grave but its effect won't be invoked
	//// starting hand
		//// starting cards
		//// give starting cards at the start of game
	// enemy
		//todo also need to record enemy's health
		//// make enemy decks for when there's no player deck recorded
		////deck saver
			//// save
				//// clean up
				//// save multiple decks
				//// with according win/loss?
		//// read decks from local
		//// populate enemy deck
	//// check cost
		//// check multiple costs
	//// draw situation
		//// no win no heart loss
	// text display in scene
		//? show cards' owner
		//// show tags on card
	        //// show effect result
			    //// need to figure out how to discern reveal zone effect and deck effect
			    //// reveal zone effect succeeded
				    //// need to implement it in all effect script
					    //// hp alter
						    //// if invoked by tag, then it doesn't have a parent card script, need to think of a different way to do tag
					    //// mana alter
					    //// card manipulation
					    //// infection
				    //// then see if it can be optimized and cleaned up more
			    //// reveal zone effect failed
			    //// deck effect succeeded
			    //// grave effect succeeded
			    //// effect target
	//// shuffle and mix decks
	//// reveal one by one
	//// reshuffle
	//// go to result screen after player pressed space
	//// migrate info display codes to a new script
	
	// effects
		//// give shield
		//// generate: make temp cards that only last 1 combat phase
			//// take out (Clone) from new card's name
		//// undertake: send cards straight to grave
		//// heart-change: change cards owner that only last 1 combat phase
			//// problem is, best strat is only having heart-change
			//// need to give heart-change cost so that the player needs other cards for it to work
		//// reborn: put cards in grave back to deck
	        //// alter hp
	        //// alter mp
	        //// show tag effects
	        // tag
			//// linger
			//// mana X
		//status effect
			//// remove status effect correctly
			//// undead: if sent to grave, put back to deck and consume status effect
				//// destroy status effect resolver 
			//// rested: add 1 if entered grave
			//// directional status effect
				//// clean up
			//// power: dmg increase
			//// heart-changed: so that it can be tracked
				//// change card parent also
			//// change mana to tag
		                //// give mana tags
		                //// check mana tags
		                //// consume mana tags
			//// infected
				//// destroy tag resolver if tag is removed
				//// deal dmg
				//// make sure this tag can only be given to cards without this tag
		//// card manipulation
			//// stage self
			//// bury self

	// cost
		//// hp
		//// mana
		//// in reveal
		//// in grave
		//// is infected
		//// is rested: no [rest]
		//// enemy card in deck to prevent cards only pay cost but no effect
		//// owner card in grave

	// trigger events
		// specific event
			//// death-rattle: if sent to grave
			//// activation
		//// use game event SO to avoid bloating card event trigger script
	                //// card activation
	                //// after shuffling
	                //// when player dealt dmg to enemy
			//// on card bought (kind of ugly, but works)
		//// put cost n effect container and effect scripts to child objects to better organize a card
		// any event
			//// need to discern between invoking me event or invoking them event
			//// document glossary
			//// when player dealt dmg to enemy
			//// after shuffling
			//// need test: linger effects straight on card
			//// avoid looping
				//// effect chain
					//// need to close chain when 1 round is finished
					//// same card, multiple loops
						//// documentation
						//// simplify effect chains
						//// made two test cards to re-create bug
						//// parent and sub chains
						//// mark chains closed
						//// check if chain closed
						//// check if effect already recorded in chain
						//// prevent effect from invoking self
					//// need to check all situations
						//// effect activating itself
						//// multiple cards loop, activating each other
						//// beforeIDealDmg event and deal dmg if dmg dealt
							//// this will bypass [effect can't invoke self] since last effect inst isn't self anymore
							//// no more beforeIDealDmg time point and game event
						//// same card, multiple loopable effects
							//// don't do this, will stack overflow
		//// test to see if checking and comparing player status can discern dmg dealer and dmg receiver
	//// end combat phase when one player's hp reached zero
	//// overtime
		//// implement func to add card in the middle of combat
		//// add fatigue


// shop
	//? give ability
		// stage self
		// bury self
	//// show player deck
	//// show shop items
	//// button prompt
	//? freeze: not essential
	//// buy
	//// buy deck size
	//// make item bought unbuyable
	//// space check
	//// show deck size
	//// reroll
	//// sell
	//// currency
	//// payday when entering shop
	//// price
	//// purses
	//? storage: not essential
	//// clear info when exiting shop phase
	//// result
	//// show result
	//// show button prompt
	

// debug
	//todo record data and write to local txt
		//// card showed time and bought time --> bought rate
		//// total combat amount and card appearance amount and win amount --> card win rate
		// card number in deck --> amount rate
	// left hp per session --> ave. hp after each combat


// housekeeping
	//// documentation
	//// document card system
	//// document overarching system