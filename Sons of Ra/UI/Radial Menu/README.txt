This is code associated with the player controller in Sons of Ra. Originally very different, approximately 9 months into 
development I had to retool it from being a button-input based layered menu into a radial menu for a rapidly approaching 
deadline. This was done with nothing but temp assets and a gif from our artist showing basic desired functionality, and 
about 20 hours of dev time over two days. The resultant UI was leagues easier for our players to interact with, and would
continue to be improved over development with more polished assets and some improved UX design, resulting in what is
shown in the provided gif.

Given another chance, I would try to do more to decouple the menu from the player controller, as much of its visual control
resides there, as well as to simplify the state logic driving it. Later additions to the game also added some complications,
as this menu was not designed with online multiplayer in mind, meaning a sort of "backdoor" functionality had to be added
to enable a some of a player's inputs to be replicated on their opponent's machine.

In spite of that, however, this code has managed to be some of our more resilient, with little core functionality needing to
change over more than two thirds of our development time.