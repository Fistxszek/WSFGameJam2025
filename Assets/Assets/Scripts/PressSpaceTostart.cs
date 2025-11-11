using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PressSpaceTostart : MonoBehaviour
{
   [SerializeField] private GameObject SpaceIndicator;
   private IEnumerator Assign()
   {
      while (!GameManager.Instance)
      {
         yield return null;
      }

      GameManager.Instance.controls.Movement.StartStop.started += OnSpaceDestroy;
   }

   private void OnEnable()
   {
      StartCoroutine(Assign());
   }

   private void OnDisable()
   {
      GameManager.Instance.controls.Movement.StartStop.started -= OnSpaceDestroy;
   }

   public void OnSpaceDestroy(InputAction.CallbackContext context)
   {
      Destroy(SpaceIndicator);
      Destroy(gameObject);
   }
}
