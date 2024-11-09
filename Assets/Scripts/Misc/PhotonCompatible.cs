using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using System.Reflection;
using System;
using System.Linq.Expressions;

[RequireComponent(typeof(PhotonView))]
public class PhotonCompatible : MonoBehaviour
{
    Dictionary<string, MethodInfo> methodDictionary = new();
    public PhotonView pv { get; private set; }
    protected Type bottomType;

    protected virtual void Awake()
    {
        pv = GetComponent<PhotonView>();
    }

    public void StringParameters(string methodName, object[] parameters)
    {
        MethodInfo info = FindMethod(methodName);
        if (info == null)
            Debug.Log($"{this.name} - {methodName} failed");

        if (info.ReturnType == typeof(IEnumerator))
            StartCoroutine((IEnumerator)info.Invoke(this, parameters));
        else
            info.Invoke(this, parameters);
    }

    public (string instruction, object[] parameters) TranslateFunction(Expression<Action> expression)
    {
        if (expression.Body is MethodCallExpression methodCall)
        {
            ParameterInfo[] parameters = methodCall.Method.GetParameters();
            object[] arguments = new object[methodCall.Arguments.Count];

            for (int i = 0; i < methodCall.Arguments.Count; i++)
            {
                var argumentExpression = Expression.Lambda(methodCall.Arguments[i]).Compile();
                arguments[i] = argumentExpression.DynamicInvoke();
                //Debug.Log($"{parameters[i].Name}, {parameters[i].ParameterType.Name}, {arguments[i]}");
            }

            return (methodCall.Method.Name, arguments);
        }
        return (null, null);
    }

    public void DoFunction(Expression<Action> expression, RpcTarget affects = RpcTarget.All)
    {
        (string instruction, object[] parameters) = this.TranslateFunction(expression);

        MethodInfo info = FindMethod(instruction);
        if (PhotonNetwork.IsConnected)
        {
            pv.RPC(info.Name, affects, parameters);
        }
        else if (affects != RpcTarget.Others)
        {
            if (info.ReturnType == typeof(IEnumerator))
                StartCoroutine((IEnumerator)info.Invoke(this, parameters));
            else
                info.Invoke(this, parameters);
        }
    }

    public void DoFunction(Expression<Action> expression, Photon.Realtime.Player specificPlayer)
    {
        (string instruction, object[] parameters) = this.TranslateFunction(expression);

        MethodInfo info = FindMethod(instruction);
        if (PhotonNetwork.IsConnected && specificPlayer != null)
            pv.RPC(info.Name, specificPlayer, parameters);
        else if (info.ReturnType == typeof(IEnumerator))
            StartCoroutine((IEnumerator)info.Invoke(this, parameters));
        else
            info.Invoke(this, parameters);
    }

    protected MethodInfo FindMethod(string methodName)
    {
        if (methodDictionary.ContainsKey(methodName))
            return methodDictionary[methodName];

        MethodInfo method = null;
        Type currentType = bottomType;

        try
        {
            while (currentType != null && method == null)
            {
                method = currentType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                if (method == null)
                    currentType = currentType.BaseType;
                else
                    break;
            }
            if (method != null && (method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator)))
            {
                methodDictionary.Add(methodName, method);
            }
        }
        catch (ArgumentException) { }
        catch
        {
            Debug.LogError($"{this.name}: {methodName} failed");
        }
        return method;
    }

    public void DestroyMe()
    {
        try
        {
            if (!PhotonNetwork.IsConnected)
                Destroy(this.gameObject);
            else if (pv.AmOwner)
                PhotonNetwork.Destroy(this.pv);
        }
        catch
        {

        }
    }
}