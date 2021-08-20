This is a selection of the code that controls the AI Opponent in Sons of Ra. The team decided to add
it fairly late in development, so it is not designed to be incredibly sophisticated, as far as RTS AIs
go. Instead, we focused on something that would be able to make relatively rational decisions on a 
limited set of information, and take simple actions in response. As such, algorithms like MCTS were
foregone.

Generally, the way it works is by analyzing the board state and developing a simple picture of it internally.
For isntance, it uses the lane waypoints to map lanes, and periodically checks for units using the waypoints as guides. It
then assigns a general condition value to that lane based on the position of enemy and allied units.
Units close to their destination (for either team) have higher weight, so seeing lots of close
enemies or lots of allies near the enemy base will result in higher condition values one way or the other.

The AI will use similar scanning methodology to judge where to place towers, or which of its abilities are
best to be used.