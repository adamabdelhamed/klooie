namespace klooie.Gaming;

/// <summary>
/// Minimal contract for any broad-phase spatial structure (uniform grid, quadtree …).
/// </summary>
public interface ISpatialIndex
{
    /// <summary>Adds <paramref name="obj"/> to the index. Call once after creation.</summary>
    void Insert(GameCollider obj);

    /// <summary>Removes <paramref name="obj"/> from the index. Call before disposal.</summary>
    void Remove(GameCollider obj);

    /// <summary>
    /// Notify the index that <paramref name="obj"/> moved.  
    /// Pass the previous bounds so the index can relocate efficiently.
    /// </summary>
    void Update(GameCollider obj, in RectF oldBounds);

    /// <summary>
    /// Returns all objects whose bucket(s) overlap <paramref name="aabb"/>.  
    /// Enumeration alloc-free and safe against inserts/removes on the same message-loop thread.
    /// </summary>
    void Query(in RectF aabb, CollidableBuffer buffer);
    void Query(in RectF aabb, ObstacleBuffer buffer);

    void Query(in RectF aabb, CollidableBuffer buffer, GameCollider except);
    void Query(in RectF aabb, ObstacleBuffer buffer, GameCollider except);

    void EnumerateAll(ObstacleBuffer buffer);

    bool IsExpired(GameCollider c);
}