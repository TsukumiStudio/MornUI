using System;
using System.Collections.Generic;

namespace MornLib
{
    internal sealed class MornUIEventSystem
    {
        private readonly Dictionary<MornUILayoutNode, Dictionary<string, List<Action>>> _listeners = new();

        public void AddEventListener(MornUILayoutNode node, string eventType, Action handler)
        {
            if (!_listeners.TryGetValue(node, out var nodeEvents))
            {
                nodeEvents = new Dictionary<string, List<Action>>();
                _listeners[node] = nodeEvents;
            }

            if (!nodeEvents.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Action>();
                nodeEvents[eventType] = handlers;
            }

            handlers.Add(handler);
        }

        public void RemoveEventListener(MornUILayoutNode node, string eventType, Action handler)
        {
            if (!_listeners.TryGetValue(node, out var nodeEvents)) return;
            if (!nodeEvents.TryGetValue(eventType, out var handlers)) return;
            handlers.Remove(handler);
        }

        /// <summary>
        /// Dispatch an event to a node, bubbling up to ancestors.
        /// Returns true if any handler was invoked.
        /// </summary>
        public bool DispatchEvent(MornUILayoutNode target, string eventType)
        {
            var handled = false;
            var node = target;
            while (node != null)
            {
                if (_listeners.TryGetValue(node, out var nodeEvents))
                {
                    if (nodeEvents.TryGetValue(eventType, out var handlers))
                    {
                        foreach (var handler in handlers)
                        {
                            handler.Invoke();
                            handled = true;
                        }
                    }
                }

                node = node.Parent;
            }

            return handled;
        }

        public void Clear()
        {
            _listeners.Clear();
        }
    }
}
