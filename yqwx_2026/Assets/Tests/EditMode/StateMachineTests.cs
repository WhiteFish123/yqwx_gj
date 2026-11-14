using System.Collections.Generic;
using NUnit.Framework;

public class StateMachineTests
{
    private class FakeState : EntityState
    {
        private readonly string stateName;
        private readonly List<string> log;

        public FakeState(StateMachine stateMachine, string stateName, List<string> log)
            : base(stateMachine, null, null)
        {
            this.stateName = stateName;
            this.log = log;
        }

        public override void Enter()
        {
            base.Enter();
            log.Add("enter:" + stateName);
        }

        public override void Exit()
        {
            base.Exit();
            log.Add("exit:" + stateName);
        }
    }

    private class CountingState : EntityState
    {
        public int updateCount;

        public CountingState(StateMachine stateMachine) : base(stateMachine)
        {
        }

        public override void Update()
        {
            base.Update();
            updateCount++;
        }
    }

    [Test]
    public void Initialize_EntersStartState()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);

        machine.Initialize(idle);

        Assert.AreSame(idle, machine.currentState);
        Assert.AreEqual(new[] { "enter:idle" }, log);
    }

    [Test]
    public void ChangeState_ExitsOldThenEntersNew()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        FakeState move = new FakeState(machine, "move", log);
        machine.Initialize(idle);

        bool changed = machine.ChangeState(move);

        Assert.IsTrue(changed);
        Assert.AreSame(move, machine.currentState);
        Assert.AreEqual(new[] { "enter:idle", "exit:idle", "enter:move" }, log);
    }

    [Test]
    public void ChangeState_ToSameState_ReturnsFalseAndDoesNothing()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        machine.Initialize(idle);

        bool changed = machine.ChangeState(idle);

        Assert.IsFalse(changed);
        Assert.AreEqual(new[] { "enter:idle" }, log);
    }

    [Test]
    public void ChangeState_ToNull_ReturnsFalse()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        machine.Initialize(idle);
        bool raised = false;
        machine.OnStateChanged += state => raised = true;

        bool changed = machine.ChangeState(null);

        Assert.IsFalse(changed);
        Assert.IsFalse(raised);
        Assert.AreSame(idle, machine.currentState);
        Assert.AreEqual(new[] { "enter:idle" }, log);
    }

    [Test]
    public void SwitchOff_BlocksFurtherChanges()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        FakeState move = new FakeState(machine, "move", log);
        machine.Initialize(idle);
        machine.SwitchOffStateMachine();

        bool changed = machine.ChangeState(move);

        Assert.IsFalse(changed);
        Assert.AreSame(idle, machine.currentState);
        Assert.AreEqual(new[] { "enter:idle" }, log);
    }

    [Test]
    public void Initialize_Twice_ThrowsAndKeepsLockedState()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        FakeState move = new FakeState(machine, "move", log);
        machine.Initialize(idle);

        Assert.Throws<System.InvalidOperationException>(() => machine.Initialize(move));
        Assert.AreSame(idle, machine.currentState);
        Assert.AreEqual(new[] { "enter:idle" }, log);
    }

    [Test]
    public void Initialize_NullStartState_Throws()
    {
        StateMachine machine = new StateMachine();

        Assert.Throws<System.ArgumentNullException>(() => machine.Initialize(null));
    }

    [Test]
    public void ChangeState_RaisesOnStateChangedWithNewState()
    {
        StateMachine machine = new StateMachine();
        List<string> log = new List<string>();
        FakeState idle = new FakeState(machine, "idle", log);
        FakeState move = new FakeState(machine, "move", log);
        machine.Initialize(idle);
        EntityState raised = null;
        machine.OnStateChanged += state => raised = state;

        machine.ChangeState(move);

        Assert.AreSame(move, raised);
    }

    [Test]
    public void UpdateActiveState_CallsCurrentStateUpdate()
    {
        StateMachine machine = new StateMachine();
        CountingState idle = new CountingState(machine);
        machine.Initialize(idle);

        machine.UpdateActiveState();

        Assert.AreEqual(1, idle.updateCount);
    }
}
