using UnityEngine;

public abstract class State
{
    protected HFSMController controller;
    public State(HFSMController controller) => this.controller = controller;

    public virtual void OnEnter() { }
    public virtual void OnExit() { }
    public virtual void Update() { }
}
