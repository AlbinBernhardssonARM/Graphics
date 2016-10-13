using System;
using System.Collections.Generic;

namespace UnityEngine.Graphing
{
    public delegate void OnNodeModified(INode node);

    public interface INode
    {
        OnNodeModified onModified { get; set; }
        IGraph owner { get; set; }
        Guid guid { get; }
        Guid RewriteGuid();
        string name { get; set; }
        bool canDeleteNode { get; }
        IEnumerable<T> GetInputSlots<T>() where T : ISlot;
        IEnumerable<T> GetOutputSlots<T>() where T : ISlot;
        IEnumerable<T> GetSlots<T>() where T : ISlot;
        void AddSlot(ISlot slot);
        void RemoveSlot(int slotId);
        SlotReference GetSlotReference(int slotId);
        T FindSlot<T>(int slotId) where T : ISlot;
        T FindInputSlot<T>(int slotId) where T : ISlot;
        T FindOutputSlot<T>(int slotId) where T : ISlot;
        IEnumerable<ISlot> GetInputsWithNoConnection();
        DrawingData drawState { get; set; }
        bool hasError { get; }
        void ValidateNode();
        void UpdateNodeAfterDeserialization();
    }
}
