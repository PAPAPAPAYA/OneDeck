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
    //// draw situation
        //// no win no heart loss
    //// text display in scene
    //// shuffle and mix decks
    //// reveal one by one
    //// reshuffle
    //// go to result screen after player pressed space
    //// migrate info display codes to a new script
    // effects
        //// alter hp
        //// alter mp
        //// show tag effects
        // resolve tags
            //// infected
                //// deal dmg
                //// make sure this tag can only be given to cards without this tag
            // 
        // stage
            //// stage self
            //// bury self
            //
    // trigger events
        //todo use game event SO to avoid bloating card event trigger script
        //// when player dealt dmg to enemy
        //// activation
        //// after shuffling
        //
    // lingering effects (mechanically there's no special lingering effects, all effects goes through card event trigger)
        ////todo need test: linger effects straight on card
        //todo show linger effects in combat info
        ////todo avoid looping
            // effect chain
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