
# Overview
This project renders a ConsoleBitmap as a 3D board (cells + extruded glyphs/shapes) using Veldrid. There is a project called klooie.ThreeD.Sample in this repo that shows how you can use it.

## Status

Experimental - This is a playground for me to learn about 3D rendering and Veldrid, and to see if I can get a 3D renderer working for klooie. It's not really intended for production use in its current state, but it's a fun experiment and I learned a lot building it.

## Features
- Implements a custom ITerminalHost using Veldrid so that klooie stops expecting a real console and instead delegates the rendering to this project's implementation.
- The renderer renders the prepared ConsoleBitmap on every frame along with some knowledge about which ConsoleControl painted to which pixel, which should allow for using the floating point coordinates of the controls that painted to the console.
	- Admittedly the ConsoleControl owner feature is not working and it's kind of a bummer because I instrumented Container to have this ICompositionObserver interface that runs on every composition and its still not working. But I ran out of time on this one.
		- The intent of the system was that we can run a little routine for for every ConsoleBitmap pixel (type ConsoleCharacter) that we're rendering on the 3d path.
			- We see which ConsoleControl produced that ConsoleCharacter. 
			- If the owning ConsoleControl has float coordinates (ConsoleControl.Left and ConsoleControl.Top) that do not equal their integer coordinates (ConsoleControl.X and ConsoleControl.Y)  then we use the fractional part to offset the shape that we're drawing for that pixel.
			- The idea is that the physics system (demonstrated in klooie.ThreeD.Sample) would then render moving controls without integer snapping having every few frames. 
- The renderer has a 2d glyph path and a 3d shape path. Any char glyph that is not registered in the ShapeRegistry gets rendered as 2d. 
- The renderer handles screen resizing, but resizing is very expensive and takes a few seconds. We have not done a great job of optimizing the initialization path so it does lots of work on resize.
- There is a feature that generates 3d character shapes from any char. It works pretty well for how much time I put into it. But it doesn't look great when the text is small (like default console sizes) which made it a dealbreaker for the project I started this for. It does look pretty cool when the text is larger though, so maybe there is some use for it in other contexts.
	- There is a good chance this can be improved once I learn more about 3d. My understanding is that my use of lighting, depth, edge sharpness, and other factors contribute to the text not looking great at small sizes. I also just kind of threw it together with a lot of hardcoded values and no real understanding of how to make it look good, so there is definitely room for improvement.
- There is a zoom feature implemented, but not hooked up anywhere right now. It's designed to take values from an XInput joystick that have been normalized and then modify the BoardZoom property that lives in the host. The idea is that you could use a gamepad joystick to zoom in and out of the console, which would be a pretty cool feature for a 3d console renderer.
	- The zoom feature did work in the main project where I was developing it as of the time of porting it back into the klooie repo. But even then it had limitations. 
	- It correctly zoomed in and out, and correctly modified the size of the underlying bitmap to occupy the screen real estate given the window size and zoom level. However, it suffers from the same integer snapping issue that integer aligned ConsoleBitmap cells present so the zoom is not smooth and jitters as integer rounding thresholds are crossed. If we ever fix all the other problems in this project it would be cool to use a technique where we have a fractional component that shifts the 3d camera and compensates for the jitter during SyncSize. That could give us the best of both worlds where we correctly resize the underlying bitmap as needed while keeping the visual smooth.


## How it plugs in
The main way this plugs in is through the ITerminalHost interface. The ThreeDRenderer class implements this interface and is responsible for rendering the ConsoleBitmap to the screen using Veldrid. Klooie knows nothing about the types in this project. 

This keeps klooie pure and allows a klooie ConsoleApp to be built and rendered by any ITerminalHost implementation, whether it's this 3d one or some other implementation that renders to a real console or something else entirely.
	
## Vision
The vision of this project is to create a 3d chessboard-like representation of a ConsoleBitmap. 

- Background colors honor the ConsoleCharacter.BackgroundColor at each cell position and the foreground glyphs and colors have nice 3d representations. 
- Non whitespace characters map to either 2d flat glyphs or 3d shapes that extrude above the board.
- The camera can be manipulated to give different perspectives on the board, and the lighting can be adjusted to create different moods.
- The end result is a diorama with cells, shapes, and flat glyphs that represent the ConsoleBitmap in a way that is both visually interesting and still recognizable as the original console output.

The first iteration of this library came pretty close. The biggest things in the way of realizing the vision are:

- The quality of small text rendering - Without this, nothing else makes sense. 
- The integer snapping issues - Bringing smooth motion as we escape the integer grid of the ConsoleBitmap (constraint imposed by normal consoles) is a huge part of the vision, and the controls already support floating point numbers. This set of improvements would be a huge win.
- We need to build a real 3d camera that plays well with the 2d space. Currently we just have the zoom from perfectly top down, and even it suffers from the integer snapping issues. 
- The lighting and materials could use a lot of work. Right now I don't even really know what's in there. The LLM wrote something that kind of works, but it surely contributes to the poor small text and would need to be re-imagined for the final vision.
- The shape generation is pretty basic, especially the extruded char shape generation. It's impressive that it work as well as it does, but a keen 3d eye should really take a closer look and make it cohesive with the lighting, camera, and small text goals to make it better. Text is the heart and soul of klooie so this layer can't be just ok.
- One small nit is that VeldridTerminalHost requires an init to be called before new ConsoleApp() since ConsoleApp relies on ConsoleProvider.Current. Ideally I would finish the abstraction and make ConsoleApp only rely on TerminalHost vis TerminalHost.Console (or something like that).

Start with this list if we ever pick this project up again.

## Screenshot

At the time of writing this document I took a screenshot of what the sample produces so that a reader (or AI agent) can get a deeper understanding of the current state.
**external\klooie\src\klooie.ThreeD.Sample\klooie.3d.png**
