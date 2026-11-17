using UnityEngine;

namespace Outpost.Enemy
{
    /// <summary>
    /// Target selain player/generator yang boleh diserang zombie (NPC sekutu di mode Final).
    /// EnemyBrain hanya memakai daftar EnemyBrain.Allies; di mode lain daftar itu kosong.
    /// </summary>
    public interface IAllyTarget
    {
        Transform TargetTransform { get; }
        bool IsTargetable { get; }
        void TakeHit(float damage, Vector3 direction);
    }
}
