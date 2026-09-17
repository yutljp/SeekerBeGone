using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeekerBeGone;

// Drives the replacement visual's Animator from the hidden original's Animator every frame.
internal class AnimatorMirror : MonoBehaviour
{
    private static readonly int s_attackTag = Animator.StringToHash("attack");
    private static readonly Dictionary<AnimationClip, bool> s_isAttackClip = new();
    private static readonly List<AnimatorClipInfo> s_clipBuffer = new();

    private Animator m_src;
    private Animator m_dst;
    private AnimatorControllerParameter[] m_sharedParams;
    private string m_attackTrigger;
    private bool m_wasAttacking;

    public void Setup(Animator src, Animator dst)
    {
        m_src = src;
        m_dst = dst;
        m_dst.applyRootMotion = false;
        m_src.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        m_dst.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var srcParams = new Dictionary<int, AnimatorControllerParameterType>();
        foreach (var p in src.parameters)
            srcParams[p.nameHash] = p.type;

        var shared = new List<AnimatorControllerParameter>();
        foreach (var p in dst.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger)
            {
                if (m_attackTrigger == null && p.name.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0)
                    m_attackTrigger = p.name;
            }
            else if (srcParams.TryGetValue(p.nameHash, out var srcType) && srcType == p.type)
            {
                shared.Add(p);
            }
        }
        m_sharedParams = shared.ToArray();
    }

    private void Update()
    {
        if (m_src == null || m_dst == null || !m_src.isActiveAndEnabled)
            return;

        foreach (var p in m_sharedParams)
        {
            switch (p.type)
            {
                case AnimatorControllerParameterType.Float:
                    m_dst.SetFloat(p.nameHash, m_src.GetFloat(p.nameHash));
                    break;
                case AnimatorControllerParameterType.Bool:
                    m_dst.SetBool(p.nameHash, m_src.GetBool(p.nameHash));
                    break;
                case AnimatorControllerParameterType.Int:
                    m_dst.SetInteger(p.nameHash, m_src.GetInteger(p.nameHash));
                    break;
            }
        }

        // Triggers can't be mirrored directly; fire the replacement's attack when the source enters an attack state.
        var attacking = IsAttacking();
        if (attacking && !m_wasAttacking && m_attackTrigger != null)
            m_dst.SetTrigger(m_attackTrigger);
        m_wasAttacking = attacking;

        m_dst.speed = m_src.speed;
    }

    // Creature attack states are rarely tagged, so also recognise them by the playing clip's name.
    private bool IsAttacking()
    {
        for (var layer = 0; layer < m_src.layerCount; layer++)
        {
            if (m_src.GetCurrentAnimatorStateInfo(layer).tagHash == s_attackTag)
                return true;

            m_src.GetCurrentAnimatorClipInfo(layer, s_clipBuffer);
            foreach (var info in s_clipBuffer)
            {
                if (info.clip != null && IsAttackClip(info.clip))
                    return true;
            }
        }
        return false;
    }

    private static bool IsAttackClip(AnimationClip clip)
    {
        if (!s_isAttackClip.TryGetValue(clip, out var isAttack))
        {
            isAttack = clip.name.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0;
            s_isAttackClip[clip] = isAttack;
        }
        return isAttack;
    }
}
