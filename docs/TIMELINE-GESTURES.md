# Timeline Gestures

The professional timeline supports three direct editing gestures while keeping the existing editor command gateway as the mutation boundary.

- Drag the body of a clip horizontally to move it on its existing track.
- Drag near the left or right edge of a clip to trim that edge.
- Double-click inside a clip to split it at the clicked timeline position.

Dragging never changes a clip's track. Cross-track drag-and-drop is intentionally not supported.

The `Snap 1s` control applies one-second snapping to direct move and trim gestures, and rejected edits leave the project unchanged.
