using Content.Shared.Inventory.Events;
using Content.Server.Clothing.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.NPC.Components;
using Robust.Shared.Random;

namespace Content.Server.Clothing.Systems;

public sealed class ClothingFactionExplosionSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingFactionExplosionComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<ClothingFactionExplosionComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<ClothingFactionExplosionComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(EntityUid uid, ClothingFactionExplosionComponent component, ComponentInit args)
    {
        component.TimerDelay = _random.NextFloat(component.MinRandomTime, component.MaxRandomTime);
        component.TimerDuration = component.VVTimerDuration;
        if (_random.NextFloat(0f, 1f) > component.Chance)
        {
            component.VVTimerEnabled = false;
        }
    }

    private void OnGotEquipped(EntityUid uid, ClothingFactionExplosionComponent component, GotEquippedEvent args)
    {
        var sysMan = IoCManager.Resolve<IEntityManager>();
        if (!sysMan.TryGetComponent<FactionComponent>(args.Equipee, out var factionComponent))
            return;
        var ownerIsFriendly = false;
        if (factionComponent != null)
        {
            foreach (var faction in factionComponent.Factions)
            {
                if (component.Faction == faction)
                    ownerIsFriendly = true;
            }
        }
        if (!ownerIsFriendly)
        {
            component.TimerOn = true;
            if (component.AnnouncementWas)
            {
                component.WearCount += 1;
                if (component.WearCount >= component.WearCountPermanentExplosion)
                {
                    DoExplode(uid);
                }
            }
        }
    }

    private void OnGotUnequipped(EntityUid uid, ClothingFactionExplosionComponent component, GotUnequippedEvent args)
    {
        if (!component.CountdownWas)
        {
            if (component.AnnouncementWas && component.WearCount < component.WearCountMax)
            {
                SayMessage(uid, Loc.GetString("clothing-faction-explosion-unknown-dna-lose"));
                component.Timer = component.TimerDelay - 1f;
                component.TimerDuration = component.VVTimerDuration;
                component.AnnouncementWarnig = false;
                component.AnnouncementMessage = false;
            }
            component.TimerOn = false;
            component.CountdownOn = false;
        }
    }

    private void DoExplode(EntityUid coords)
    {
        var sysMan = IoCManager.Resolve<IEntitySystemManager>();
        sysMan.GetEntitySystem<ExplosionSystem>().QueueExplosion(coords, "Default", 13, 13, 13);
        QueueDel(coords);
    }

    private void SayMessage(EntityUid uid, string msg)
    {
        _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ClothingFactionExplosionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.TimerOn && comp.VVTimerEnabled)
            {
                comp.Timer += frameTime;
                if (comp.Timer >= comp.TimerDelay && !comp.AnnouncementWarnig)
                {
                    SayMessage(uid, Loc.GetString("clothing-faction-explosion-unknown-dna"));
                    comp.AnnouncementWarnig = true;
                    comp.AnnouncementWas = true;
                }
                if (comp.Timer >= comp.TimerDelay + 2f && !comp.AnnouncementMessage)
                {
                    SayMessage(uid, Loc.GetString("clothing-faction-explosion-start-detonation"));
                    comp.AnnouncementMessage = true;
                    comp.CountdownOn = true;
                }
                if (comp.Timer >= comp.TimerDelay + 2f && !comp.CountdownOn)
                {
                    comp.CountdownOn = true;
                }
                if (comp.CountdownOn)
                {
                    if (comp.TimerDuration < 0)
                    {
                        DoExplode(uid);
                    }
                    comp.TimerCountdown += frameTime;
                    if (comp.TimerCountdown >= comp.CountdownDelay)
                    {
                        comp.CountdownWas = true;
                        SayMessage(uid, comp.TimerDuration.ToString());
                        comp.TimerDuration -= 1;
                        comp.TimerCountdown = 0f;
                    }
                }
            }
        }
    }
}
