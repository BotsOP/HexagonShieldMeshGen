using UnityEngine;
using UnityEngine.InputSystem;

public class MouseCollision : MonoBehaviour
{
    public Material mat;
    void Update()
    {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out hit)) {
            mat.SetVector("_Hit", hit.point);
        }
    }
}
