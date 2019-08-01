﻿using SparrowServer.Attributes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SparrowServer {
	class RequestStruct {
		public RequestStruct (MethodInfo _method) {
			m_method = _method;
			if (!m_method.IsStatic)
				throw new Exception ("Method must be static / 方法必须声明为静态");
			foreach (var _param in m_method.GetParameters ()) {
				var _attrs = (from p in _param.GetCustomAttributes () where p is WEBParam.IWEBParam select p as WEBParam.IWEBParam);
				if (_attrs.Count () > 0) {
					m_params.Add ((null, _attrs.First ().Name));
				} else if (_param.ParameterType == typeof (FawRequest)) {
					m_params.Add ((null, "FawRequest"));
				} else if (_param.ParameterType == typeof (FawResponse)) {
					m_params.Add ((null, "FawResponse"));
				} else {
					m_params.Add ((_param.ParameterType, _param.Name));
				}
			}
		}

		public void process (FawRequest _req, FawResponse _res) {
			var _params = new object [m_params.Count];
			bool _ignore_return = false;
			for (int i = 0; i < m_params.Count; ++i) {
				if (m_params [i].Item1 != null) {
					_params [i] = _req.get_type_value (m_params [i].Item1, m_params [i].Item2);
				} else {
					switch (m_params [i].Item2) {
						case "FawRequest":
							_params [i] = _req;
							break;
						case "FawResponse":
							_params [i] = _res;
							_ignore_return = true;
							break;
						case "IP":
							_params [i] = _req.m_ip;
							break;
						case "AgentIP":
							_params [i] = _req.m_agent_ip;
							break;
						default:
							throw new Exception ("Request parameter types that are not currently supported / 暂不支持的Request参数类型");
					}
				}
			}
			try {
				var _ret = m_method.Invoke (null, _params);
				//if (_ret.GetType () == typeof (Task<>)) // 始终为False
				if (_ret is Task _t) {
					if (_ret.GetType () != typeof (Task)) {
						_ret = _ret.GetType ().InvokeMember ("Result", BindingFlags.GetProperty, null, _ret, null);
					} else {
						_t.Wait ();
						_ret = null;
					}
				}
				if (!_ignore_return) {
					if (_ret is byte _byte) {
						_res.write (_byte);
					} else if (_ret is byte [] _bytes) {
						_res.write (_bytes);
					} else {
						string _content = _ret.to_str ();
						object _o;
						if (_content == "") {
							_o = new { result = "success" };
						} else if (_content[0] != '[' && _content[0] != '{') {
							_o = new { result = "success", content = _content };
						} else {
							_o = new { result = "success", content = JToken.Parse (_content) };
						}
						_res.write (_o.to_json ());
					}
				}
			} catch (TargetInvocationException ex) {
				throw ex.InnerException;
			}
		}

		MethodInfo m_method = null;
		private List<(Type, string)> m_params = new List<(Type, string)> ();
	}
}
