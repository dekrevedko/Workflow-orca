using OrcaCore;

public static class Forbidden
{
    public static void MustNotCompile(
        EphemeralNestedBuilder<object, object> en, DurableNestedBuilder<object, object> dn,
        EphemeralBranchBuilder<object, object> eb, DurableBranchBuilder<object, object> db,
        EphemeralItemBuilder<object, object> ei, DurableItemBuilder<object, object> di,
        DurableLeaseWorkflowBuilder<object, object> lw, DurableLeaseNestedBuilder<object, object> ln,
        DurableLeaseBranchBuilder<object, object> lb, DurableLeaseItemBuilder<object, object> li,
        EphemeralWorkflowBuilder<object, object> er,
        ResourceLeaseRequest request)
    {
        en.Parallel<object>(_ => { }); dn.Parallel<object>(_ => { });
        eb.Parallel<object>(_ => { }); db.Parallel<object>(_ => { });
        ei.Parallel<object>(_ => { }); di.Parallel<object>(_ => { });
        en.ForEach<object, object, object>(default!, default!, default!, default!);
        dn.ForEach<object, object, object>(default!, default!, default!, default!);
        en.While(default!, default!); dn.While(default!, default!);
        lw.Parallel<object>(_ => { }); ln.Parallel<object>(_ => { }); lb.Parallel<object>(_ => { }); li.Parallel<object>(_ => { });
        lw.ForEach<object, object, object>(default!, default!, default!, default!);
        ln.AcquireResources(request, _ => { }); lb.AcquireResources(request, _ => { }); li.AcquireResources(request, _ => { });
        lw.ContinueAsNew(_ => new object()); ln.ContinueAsNew(_ => new object());
        er.ContinueAsNew(_ => new object());
        er.WaitLong(default!, default!); er.Yield(); er.WhenFirst(default!); er.RunChild(default!); er.RunExternalJob(default!);
    }
}
