You are helping create data for a narrative-driven “life simulation toy.”
The player moves on a grid. Each grid cell contains a concept (not necessarily an object—can be emotional, metaphorical, societal, abstract, etc.). When the player steps next to a concept, they can perform one of its actions, which get recorded in their life log. When the player dies, all logged actions are sent to an LLM to synthesize their life story.
The story may be: sci-fi, fantasy, detective, slice-of-life, comedy, etc., so the concepts must be versatile.

Generate concepts using this exact format:

*ConceptName:
  icon(spriteName)        
  categories(cat1, cat2, ...)
  color(#RRGGBB)
  action(ActionName[display verb], duration) 
  action(ActionName, [min-max], optionalConditions...)
  touch_death(CauseOfDeath)       # optional

EXAMPLE CONCEPT
*Happyness:
  icon(emote happy)        
  categories(feelings)   
  color(#FFFF00)      
  action(Embrace[embraced], 1)
  action(Reject[rejected], 1)

Rules & Notes
- display verb is optional, but it expressed how the action should be displayed in text form. For example, if the example above, this will show up as "embraced concept of happyness" in the text. Note that the display name shouldn't include the concept itself (so it shouldn't be Embrace[embraced happyness], but just "Embrace[embraced]).
- spriteName must match one of the sprite names from the provided list.
- categories are custom tags used to enforce conditions.
- color is the drawing color for the icon (hex RGB).
- action(name, duration)
- duration can be a single number (years) or a range [min-max].
- avoid calling something "ConceptOf...", and such stuff, since it will be used when displayed (for example an action like "action(Embrace[embraced])" on a concept called "Happyness" will display "embraced happyness", which is more correct and pleasant)

Actions may include conditions such as:
  - RequireAge(n)
  - RequireCategory(catA, catB)
Try to make the actions be coherent with the age (using the RequireAge condition) - for example, if an action is "Plan Trip", then it makes little sense that pre-18 years old that will happen, so do in that case a RequireAge(18).
Guarantee at least one action that has no conditions, unless it's has touch_death.
Some concepts may have touch_death(reason), meaning touching them kills the player.
Concepts do not need to be physical. They may be abstract, metaphorical, emotional, societal, mythological, etc.
Categories must be consistent: If an action requires RequireCategory(X), make sure some other concept grants that category.

When asked, generate a list of N concepts (example: 5 concepts).
Each concept must include:

A selected icon from the provided list
Thoughtful categories
A meaningful color (tone fitting the concept)
Several actions with durations or ranges
Optional lethal touch effects
Make concepts story-rich, versatile, and suitable for wildly different genres.


When outputing the concepts, show it as code so that we can easily copy&paste it.
