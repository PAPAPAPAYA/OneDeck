// devlog

//! quirks
	//! deck and grave effects don't show fail message, would be too much info? or too fewer surprises?
	//! tag damage counts as tag owner card's damage
	//! simplified hp event to [when session owner takes dmg] and [when enemy takes dmg]
	//! after changing the combined deck, shuffle
	//! currently we only use round num to match decks
	//! 43514 is [Meditate], might need to keep an eye if the instance id changes, if it changes, need to find a way so that we keep what we saved
	//! chains are closed when: [1] same card, different effect is trying to get invoked; [2] waiting input confirm;
	//! chains are composed with a string of effect containers
	//! don't put multiple effect instances with loopable effect in one card, will stack overflow; put multiple loopable effects in same effect instance
	//! no beforeIDealDmg time point and game event, would stack overflow; HPAlterEffect calculates dmg before dealing dmg

// design
	// infection
		// effect
			// give infection
		// condition/cost
			// if infected
			//? for each infected
	// mana
		// effect
			// give mana
		// condition/cost
			// if X mana
	// healing
		// effect
			// heal
		// condition/cost
			// if healed
	// self-harm
		// effect
			// deal dmg to self
		// condition/cost
			// if received dmg
	// undertake
		// effect
			// send card straight to grave
		// condition/cost
			// if sent to grave
	// reborn
		// effect
			// put cards from grave to deck
		// condition/cost
			//? each time sent to grave
	// generate
		// effect
			// add temp cards to deck
		// condition/cost
			//? foreach card you/they own
	// heart change
		// effect
			// give heart change
		// condition/cost
			//? if heart changed
	// shield
		// effect
			// give shield
		// condition/cost
			//? if X shield
			//? foreach shield
	// echo (straight back to deck if cost met)
		// effect
			//? go to deck from reveal zone
		// condition/cost
			//? each time is played
	// bury
		// effect
			// send card to bottom of deck
		// condition/cost
			//? for each X happened
	// stage
		// effect
			// send card to top of deck
		// condition/cost
			//? for each X not happened


// refactoring
#region Refactorings
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

// abandoned: too much work, not economic
#region abandoned
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

// cards
	// hp alter
		//// stab: deal 1 dmg
		//// stab quickly: stage self; deal 1 dmg
		//// stab slowly: bury self; deal 1 dmg
		//// stab recklessly: deal 1 dmg to self; deal 1 dmg
		//// cursed stab: deal 2 dmg; generate 1 curse
		//// deal dmg = lost hp
	// shield
		//// each time lose hp, add shield
		//// self harm, add shield
	// tags (status effect)
		// mana
			//// meditate: mana to 3 cards
			//// inject: deal 1 dmg to self; mana to 5 cards
			//// fireball: cost 1 mana: deal 2 dmg
			//// big fireball: cost 2 mana: deal 3 dmg
		// infection
			//// explosive infection: infect 3 cards
			//// poisoned knife: deal 2 dmg; if infected: deal 2 dmg
		// power
			//// effect chain is blocking multiple power tag resolvers from increasing dmg multiple times
			//// power up: power to 3 cards
	// shiv
		//// add a shiv: add a shiv
		//// shiv: deal 1 dmg
	// grave (field)
		//// death by a thousand cuts: if in grave: when enemy received dmg: deal 1 dmg
		//// undertake: move 3 random cards straight to grave
		//// stab from the grave: when sent to grave, deal 1 dmg
	// system
		//// fatigue: deal 1 dmg to both players
		//// deck expansion: increase deck size by 1
		//// increase max hp
	// curse
		//// slippery floor: deal 1 dmg to self
	

// battle: text demo
	//// when a new round begins and the first card is enemy's, it will be sent to grave but its effect won't be invoked
	//// enemy
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
	//// text display in scene
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
		// tags (status effect)
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
				//? destroy tag resolver if tag is removed
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
		// cool down

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
	//? give tag
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
	//? record data and write to local txt

// housekeeping
	//// documentation
	//// document card system
	//// document overarching system