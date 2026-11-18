using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Placement helpers for parts inside the unit-radius species editor circle.
/// Part radii and positions are normalized to the species circle's radius.
/// </summary>
public static class PartCirclePacking
{
    public const float SpeciesRadius = 1f;
    const float PositionTolerance = 0.0001f;

    public static bool IsValidPlacement(
        Vector2 position,
        float radius,
        IEnumerable<Part> otherParts,
        Part ignoredPart = null)
    {
        if (!IsFinite(position.x) || !IsFinite(position.y)
            || !IsFinite(radius) || radius <= 0f || radius > SpeciesRadius)
        {
            return false;
        }

        float maxCenterDistance = SpeciesRadius - radius;
        if (position.sqrMagnitude > maxCenterDistance * maxCenterDistance + PositionTolerance)
        {
            return false;
        }

        if (otherParts == null)
        {
            return true;
        }

        foreach (Part other in otherParts)
        {
            if (other == null || other == ignoredPart)
            {
                continue;
            }

            if (!IsFinite(other.Radius) || other.Radius <= 0f || other.Radius > SpeciesRadius
                || !IsFinite(other.PlacementPosition.x) || !IsFinite(other.PlacementPosition.y))
            {
                return false;
            }

            float combinedRadius = radius + other.Radius;
            if ((position - other.PlacementPosition).sqrMagnitude
                < combinedRadius * combinedRadius - PositionTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds a deterministic valid position, useful for legacy saves.</summary>
    public static bool TryFindPosition(float radius, IEnumerable<Part> otherParts, out Vector2 position)
    {
        position = Vector2.zero;
        if (!IsFinite(radius) || radius <= 0f || radius > SpeciesRadius)
        {
            return false;
        }

        if (IsValidPlacement(position, radius, otherParts))
        {
            return true;
        }

        const int CandidateCount = 720;
        const float GoldenAngle = 2.39996323f;
        float maxCenterDistance = SpeciesRadius - radius;
        for (int index = 1; index <= CandidateCount; index++)
        {
            float radialFraction = Mathf.Sqrt(index / (float)CandidateCount);
            float angle = index * GoldenAngle;
            Vector2 candidate = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)) * (radialFraction * maxCenterDistance);

            if (!IsValidPlacement(candidate, radius, otherParts))
            {
                continue;
            }

            position = candidate;
            return true;
        }

        return false;
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}