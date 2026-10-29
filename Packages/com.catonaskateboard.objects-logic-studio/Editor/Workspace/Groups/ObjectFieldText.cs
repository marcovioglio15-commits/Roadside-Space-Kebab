using System;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps setting tooltips short while retaining useful instructions.</summary>
    internal static class ObjectFieldText
    {
        #region Methods

        #region Tooltips

        /// <summary>Provides plain explanations for frequently used settings.</summary>
        /// <param name="property">Setting shown by a shared control.</param>
        /// <returns>A short English tooltip.</returns>
        internal static string Tooltip(SerializedProperty property)
        {
            return property.name.ToLowerInvariant() switch
            {
                "enabled" => "Turn this on or off.",
                "name" or "interactionname" => "Give this a name.",
                "distance" or "playerdistance" => "How close the player needs to be.",
                "exitdistance" => "Move farther than this to leave the interaction.",
                "duration" => "How long it lasts, in seconds.",
                "interval" => "Seconds between repeats.",
                "queryinterval" => "Seconds between checks.",
                "cooldown" => "Wait this many seconds before it can happen again.",
                "obstaclemask" or "obstacles" => "These layers can block the interaction.",
                "targetmode" => "Choose how the player points at the object.",
                "solid hit only" or "solidhitonly" => "The cursor must hit a solid collider. Triggers do not count.",
                "centerradius" => "Allow this much room around the center of the view.",
                "worldcollisions" => "Keep the carried object from passing through walls.",
                "recoveryresponse" => "How quickly the object moves back after hitting a wall.",
                "instant" => "Move into the carry pose right away.",
                "physicalpickup" => "Let the object collide and push things during pickup.",
                "showhover" => "Keep the hover label visible while carrying this object.",
                "unlimited" => "Do not run out of items or space.",
                "capacity" => "How much this can hold.",
                "stock" => "Items available before a refill is needed.",
                "priority" => "Higher values take priority when several interactions are available.",
                "autoreturn" => "Return to the starting pose automatically.",
                "return delay" or "returndelay" => "Wait this many seconds before returning.",
                "autotiming" => "Match the effect length to the interaction.",
                "followobject" => "Keep the effect attached while the object moves.",
                "prefab" => "Choose the prefab to use.",
                _ => Short(property.tooltip)
            };
        }

        /// <summary>Explains small settings groups without repeating their titles.</summary>
        /// <param name="title">Group label.</param>
        /// <param name="fallback">Existing section explanation.</param>
        /// <returns>A short instruction describing what changes in this group.</returns>
        internal static string Group(string title, string fallback)
        {
            return title switch
            {
                "Carry pose" => "Where the object sits while carried.",
                "Pickup transition" => "How the object moves into the hand.",
                "Physical pickup" => "Let the object push and collide during pickup.",
                "Carry collisions" => "Keep carried objects clear of walls.",
                "Contact rotation" => "How the carried object turns against a surface.",
                "Hover while carried" => "Show the label while this object is held.",
                "Pickup sound" => "Sound played when the object is picked up.",
                "Collision sounds" => "Sounds played when the object hits something.",
                "Grab targeting" => "How the player points at this object to pick it up.",
                "Grab obstacles" => "Choose what can block a pickup.",
                "Release weight and drag" => "Weight and resistance after release.",
                "Release physics" => "Gravity, rotation limits and collision checks after release.",
                "Supply source" => "Where the dispensed items come from.",
                "Stock" => "How many items are available.",
                "Output mode" => "How and when items leave the dispenser.",
                "Outlet layout" => "Where dispensed items appear.",
                "Accepted items" => "Which items this container accepts.",
                "Storage capacity" => "How much this container can hold.",
                "Stored item layout" => "Where stored items sit while visible.",
                "Animation poses" => "Starting and ending positions for the moving part.",
                "Animation timing" => "How quickly the part moves and turns.",
                "Automatic return" => "Return to the starting pose after a delay.",
                "Contact participants" => "Which objects can start this contact effect.",
                "Contact timing" => "How long contact must last and how often it is checked.",
                "Effect interruption" => "What happens when contact is interrupted.",
                "Dialogue distances" => "How close the player needs to stay.",
                "Dialogue priority" => "Which dialogue wins when several are available.",
                "Dialogue sight" => "Whether the player needs to keep looking at this object.",
                "Lost sight" => "Hide dialogue when the player looks away.",
                "Dialogue replay" => "What happens when the player returns or presses again.",
                "Phrase audio" => "Voice sounds played during dialogue.",
                "Result audio" => "Sound played when dialogue finishes.",
                "Label text" => "What the label says.",
                "Label font" => "Font, size and color of the label.",
                "Text alignment" => "How text fits inside the label.",
                "Start effect" => "Effect played when the interaction starts.",
                "Effect lifetime" => "How long the effect stays visible.",
                "Effect attachment" => "Where the effect appears and whether it follows the object.",
                "Required ingredients" => "Items needed to complete the product.",
                "Product price" => "Payment for this product.",
                "Spawn source" => "Object whose completed interactions are watched.",
                "Completion rules" => "Events that allow the next spawn.",
                "Spawn outputs" => "Objects that can be spawned.",
                "Spawn timing" => "Delay before the new object appears.",
                "Spawn pose" => "Position and rotation of the new object.",
                "Spawn population" => "Limit how many spawned objects can stay alive.",
                "Offscreen removal" => "How long an object can stay out of view.",
                "Outline edges" => "Width and color of the highlighted edges.",
                "Order board" => "Display used to show these orders.",
                "Order reveal" => "Interaction that reveals the orders.",
                _ => Short(fallback)
            };
        }

        /// <summary>Uses the first two brief sentences of a longer existing explanation.</summary>
        /// <param name="text">Existing tooltip.</param>
        /// <returns>A compact explanation without cutting a sentence in half.</returns>
        internal static string Short(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            int first = text.IndexOf(". ", StringComparison.Ordinal);
            if (first < 0)
                return text;
            int second = text.IndexOf(". ", first + 2, StringComparison.Ordinal);
            return text.Length <= 150 ? text : text.Substring(0, first + 1 < 90 && second > 0 && second < 150 ? second + 1 : first + 1);
        }

        #endregion

        #endregion
    }
}
