# Physics

The klooie.Gaming namespace lets you have controls that move using velocity, represented as a speed and an angle. 

The Game class derives from ConsoleApp and has several convenience features that are useful for a game. 

1. Sets up a default collider group that all GameCollider objects will join by default. A collider group is a group of controls that have velocity and can collide with each other.
2. Implements a Pause aware delay provider that can freeze all physics objects within a collider group while the game is paused.

//#PhysicsSample