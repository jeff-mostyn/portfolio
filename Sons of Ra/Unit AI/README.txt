This is the controller code for unit behaviors in Sons of Ra. It exists as an inheritance hierarchy as follows:

			 Unit AI
			/	\
	Unit AI Catapult	Unit AI Infantry
						\
						 \
			Archer, Embalming Priest, Huntress, Shieldbearer, Spearman 

The units generally behave as state machines, and have pretty rudimentary behaviors, as due to the design of the
game, they only ever move in one direction, dealing with input as they become aware of it, and swapping states accordingly.