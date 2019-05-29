using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChakraHosting;

namespace Shiba.Scripting.Conversion
{
    public class PromiseConversion : ITypeConversion
    {
        public PromiseConversion()
        {
            ToJsValue = value =>
            {
                if (!(value is Task task)) return JavaScriptValue.Undefined;
                if (!(ShibaApp.Instance.Configuration.ScriptRuntime is DefaultScriptRuntime runtime))
                    return JavaScriptValue.Undefined;

                var promiseObj = runtime.ChakraHost.RunScript(
                    "(()=>{let promise = {}; promise.promise=new Promise((resolve,reject)=>{promise.resolve=resolve; promise.reject=reject}); return promise;})();"
                );
                var resolve = promiseObj.GetProperty(JavaScriptPropertyId.FromString("resolve"));
                var reject = promiseObj.GetProperty(JavaScriptPropertyId.FromString("reject"));
                var result = promiseObj.GetProperty(JavaScriptPropertyId.FromString("promise"));
                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        if (value.GetType().IsGenericType) //Task<>
                        {
                            dynamic dtask = task;
                            var taskResult = await dtask as object;
                            //runtime.ChakraHost.EnterContext();
                            resolve.CallFunction(result, taskResult.ToJavaScriptValue());
                            //runtime.ChakraHost.LeaveContext();
                        }
                        else //Task
                        {
                            await task;
                            //runtime.ChakraHost.EnterContext();
                            resolve.CallFunction(result, JavaScriptValue.Invalid);
                            //runtime.ChakraHost.LeaveContext();
                        }
                    }
                    catch (Exception ex)
                    {
                        //runtime.ChakraHost.EnterContext();
                        reject.CallFunction(result, JavaScriptValue.FromString(ex.Message));
                        //runtime.ChakraHost.LeaveContext();
                    }
                });

                return result;
            };

            FromJsValue = value =>
            {
                return Task.Factory.FromAsync((callback, state) =>
                {
                    var result = new AsyncResult();

                    JavaScriptValue FulfilledCallback(JavaScriptValue callee, bool call, JavaScriptValue[] arguments,
                        ushort count, IntPtr data)
                    {
                        result.SetResult(arguments.Skip(1).FirstOrDefault().ToNative());
                        callback?.Invoke(result);
                        return JavaScriptValue.Invalid;
                    }

                    JavaScriptValue RejectCallback(JavaScriptValue callee, bool call, JavaScriptValue[] arguments,
                        ushort count, IntPtr data)
                    {
                        result.SetError(arguments.Skip(1).FirstOrDefault().ValueType == JavaScriptValueType.String
                            ? arguments.FirstOrDefault().ToString()
                            : string.Empty);
                        callback?.Invoke(result);
                        return JavaScriptValue.Invalid;
                    }


                    var thenProperty = value.GetProperty(JavaScriptPropertyId.FromString("then"));
                    thenProperty.CallFunction(value, JavaScriptValue.CreateFunction(FulfilledCallback, IntPtr.Zero),
                        JavaScriptValue.CreateFunction(RejectCallback, IntPtr.Zero));
                    return result;
                }, result =>
                {
                    var asyncResult = result as AsyncResult ?? throw new ArgumentException("Result is of wrong type.");
                    if (asyncResult.HasError) throw new Exception(asyncResult.Error.ToString());
                    return asyncResult.Result;
                }, null);
            };
        }

        public Type ObjectType { get; } = typeof(Task);
        public Func<object, JavaScriptValue> ToJsValue { get; }
        public Func<JavaScriptValue, object> FromJsValue { get; }

        public bool CanConvert(JavaScriptValue value)
        {
            if (!value.HasProperty(JavaScriptPropertyId.FromString("constructor"))) return false;
            var constructor = value.GetProperty(JavaScriptPropertyId.FromString("constructor"));
            if (!constructor.HasProperty(JavaScriptPropertyId.FromString("name"))) return false;

            var name = constructor.GetProperty(JavaScriptPropertyId.FromString("name"));
            return name.ValueType == JavaScriptValueType.String && name.ToString() == "Promise";
        }
    }

    public class AsyncResult : IAsyncResult
    {
        private readonly ManualResetEvent _mre = new ManualResetEvent(false);

        public string Error { get; private set; }
        public bool HasError { get; private set; }
        public object Result { get; private set; }

        public WaitHandle AsyncWaitHandle => _mre;

        public bool CompletedSynchronously { get; } = false;

        public bool IsCompleted { get; private set; }

        public object AsyncState => throw new NotImplementedException();

        public void SetError(string error)
        {
            Error = error;
            HasError = true;
            Release();
        }

        public void SetResult(object value)
        {
            Result = value;
            Release();
        }

        private void Release()
        {
            IsCompleted = true;
            _mre.Set();
        }
    }
}