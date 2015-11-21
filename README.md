# BetterBurnTime
KSP mod for a more accurate/reliable "estimated burn time" indicator on the navball.


##How to install
Unzip into your GameData folder, like any mod.


## What this mod does
It tweaks the "estimated burn time" display on the navball so that it will be reliable and accurate. It takes into account increasing acceleration as fuel mass is burned.

####What this means:

* You will see a burn time that will be accurate to the second.
* You will never see "N/A" unless your vessel actually can't run at all (e.g. is out of fuel or has no active engines).
* Shows a warning if the maneuver would require more fuel than you have (see below).


## Why it's needed
The "estimated burn time" indicator provided by stock KSP is very rudimentary. It just keeps track of the maximum acceleration observed for the current ship in the current flight session, and then assumes that. This has several drawbacks:

* You just see "N/A" until the first time you do acceleration in the flight session.
* It doesn't take into account the fact that your acceleration will get better as you burn fuel
* It doesn't deal with engines running out of fuel, being deactivated, etc.
* It happily tells you an estimate even if you don't have the fuel to do the burn.


## Things that the mod handles

* It adds up the thrust of your engines to figure out what acceleration you can do.
* It knows how much fuel you have, how much your engines need, and which tanks are turned off (if any).
* It takes into account whether engines are active and have fuel available to run.
* It projects fuel use and compensates for increasing acceleration as fuel mass is burned.
* It uses the scarcest resource as the limit of fuel supply (e.g. if you're going to run out of LF before O)
* It takes the "infinite fuel" cheat into account, if that's turned on.
* If the burn exceeds your available dV, it shows the time in a "warning" format.
* It allows for engines that aren't parallel (e.g. if you have an active engine pointing the wrong way, it ups the estimate).


## The "insufficient fuel" warning

Normally, the mod displays estimated burn time like this (48 seconds in this example):

**Est. Burn: 48s**

If the mod decides that you don't have enough dV to do the specified maneuver, it will display the time like this instead:

**Est. Burn: (~48s)**

Note that it won't do this if you have the "infinite fuel" cheat turned on (since then you always have enough dV!)


## Caveats
There's a reason this mod is called *BetterBurnTime* and not *PerfectBurnTime*.  There are some conditions that it does *not* handle, as follows:

#### Doesn't predict staging
The mod bases its acceleration and dV estimates on your *current* configuration. It doesn't know about whether or when you're going to stage in the future.

Therefore, if you're going to be staging during the burn, this can cause a couple of inaccuracies:

* Underestimated burn time:  The mod's estimate is based only on the engines that are *currently* active. If your current stage is high-thrust and the next one will be low-thrust, then the mod will underestimate burn time because it's assuming that the whole burn will be with your current (high-thrust) engine.
* False alarm for dV warning:  The mod assumes you're going to be burning all fuel on the current stage. It doesn't allow for the mass you're going to drop when you jettison spent stages. Therefore, if you stage during the burn, the mod will underestimate how much dV your craft can handle, and may show the dV warning even though you're fine.

#### Doesn't predict fuel flow
The mod doesn't know what your fuel is going to do.  It naively assumes that all fuel on the ship (that hasn't been turned off by disabling the tank) is available to all active engines.  Therefore, there are a couple of situations it won't handle:

* If you have a multi-stage rocket, the mod assumes that *all* fuel is available to your *current* stage.  It will base its estimate of "available dV" on that.
* If you have multiple engines active now, but some of them are going to run out of fuel before others due to fuel flow issues, the mod doesn't predict that. It assumes that all fuel is available to all engines for the duration of the burn, and so it would underestimate the time in that case. (However, when the engines actually run out of fuel, the mod would immediately revise its estimate upwards.)

#### Ignores zero-density resources (e.g. electricity)
The mod assumes that any resources you have that don't have mass (e.g. electricity) are replenishable and therefore don't need to be tracked. Therefore, if you have an ion-powered craft and you're going to run out of electricity, the mod won't predict that. It will assume that you're going to have full electricity for the duration of the burn.


## Simple vs. complex acceleration
By default, this mod uses a "complex" calculation of burn time that takes into account that your acceleration will increase as you burn fuel mass.  This is what allows the mod to produce accurate burn times.

There are certain circumstances in which the mod will drop down to a "simple" calculation that just assumes constant acceleration based on your current thrust and mass:

* There is a configuration option you can use to force the mod to use only simple acceleration all the time (see "How to configure," below).
* If the "infinite fuel" cheat is turned on, the mod uses simple acceleration because no fuel will be consumed.
* If the dV required for the maneuver exceeds the mod's calculation of available dV. Then it will assume that you'll have complex acceleration until you're out of fuel, and applies simple-acceleration math beyond that point.


## How to configure

After the first time you run KSP with the mod installed, there will be a configuration file located at under this location in your KSP folder, which you can edit to adjust settings:

GameData/BetterBurnTime/PluginData/BetterBurnTime/config.xml

Currently, there is only one setting, UseSimpleAcceleration.  By default, this is set to 0, meaning that the mod will use complex acceleration in its calculations.

If you set it to 1, then you will force the mod to use simple acceleration for all its calculations all the time.

