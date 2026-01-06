// devlog
// design
	// infection
	// mana
	// healing
	// self-harm
	// undertake
	// heart change WIP
// card
	//// same structure from slash/ but expanded to support multiple costs and effects
		//// card structure
			//// cost check
			//// effect
			//// assign effect target ref
// battle: text demo
	// check cost
		// check multiple costs
	//// draw situation
		//// no win no heart loss
	//// text display in scene
	//// shuffle and mix decks
	//// reveal one by one
	//// reshuffle
	//// go to result screen after player pressed space
	//// migrate info display codes to a new script
	//todo show effect result
		// need to figure out how to discern reveal zone effect and deck effect
			// deck and grave effects don't show fail message, would be too much info? or too less surprises?
		//todo reveal zone effect succeeded
			// need to implement it in all effect script
			// then see if it can be optimized and cleaned up more
		//// reveal zone effect failed
		// deck effect succeeded
		// grave effect succeeded
		// effect target
	// effects
	        //// alter hp
	        //// alter mp
	        //// show tag effects
	        //todo change mana to tag
		// resolve tags
			//// infected
				//// deal dmg
				//// make sure this tag can only be given to cards without this tag
			//
			////todo refactor to use game event SOs
		// stage and bury
			//// stage self
			//// bury self
			// 
	// trigger events
		////todo use game event SO to avoid bloating card event trigger script
	                //// card activation
	                //// after shuffling
	                //// when player dealt dmg to enemy
			//// on card bought (kind of ugly, but works)
		//// when player dealt dmg to enemy
		//// activation
		//// after shuffling
		////todo put cost n effect container and effect scripts to child objects to better organize a card
		//
	// lingering effects (mechanically there's no special lingering effects, all effects goes through card event trigger)
		////todo need test: linger effects straight on card
		////todo avoid looping
			// effect chain
				////todo need to close chain when 1 round is finished
				// may need to check all situations
					//// effect activating itself
					// multiple cards loop, activating each other
					// same card, multiple same effects
		//// test to see if checking and comparing player status can discern dmg dealer and dmg receiver
	//// end combat phase when one player's hp reached zero
	//// overtime
		//// implement func to add card in the middle of combat
		//// add fatigue
// shop
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