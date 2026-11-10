script_call = """ScriptInstance *_script_instance = ((Object *)(this))->get_script_instance();\\
		if (_script_instance) {\\
			Callable::CallError ce;\\
			$CALLSIARGS\\
			$CALLSIBEGIN_script_instance->callp(_orblit_virtual_##m_name##_sn, $CALLSIARGPASS, ce);\\
			if (ce.error == Callable::CallError::CALL_OK) {\\
				$CALLSIRET\\
				return true;\\
			}\\
		}"""

script_has_method = """ScriptInstance *_script_instance = ((Object *)(this))->get_script_instance();\\
		if (_script_instance && _script_instance->has_method(_orblit_virtual_##m_name##_sn)) {\\
			return true;\\
		}"""

proto = """#define ORBLIT_VIRTUAL$VER($RET m_name $ARG)\\
	mutable void *_orblit_virtual_##m_name = nullptr;\\
	_FORCE_INLINE_ bool _orblit_virtual_##m_name##_call($CALLARGS) $CONST {\\
		static const StringName _orblit_virtual_##m_name##_sn = StringName(#m_name, true);\\
		$SCRIPTCALL\\
		if (_get_extension()) {\\
			if (unlikely(!_orblit_virtual_##m_name)) {\\
			    _orblit_virtual_init_method_ptr(_orblit_virtual_##m_name##_get_method_info().get_method_hash(), _orblit_virtual_##m_name, _orblit_virtual_##m_name##_sn);\\
			}\\
			if (_orblit_virtual_##m_name != reinterpret_cast<void*>(_INVALID_ORBLIT_VIRTUAL_FUNC_ADDR)) {\\
				$CALLPTRARGS\\
				$CALLPTRRETDEF\\
				if (_get_extension()->call_virtual_with_data) {\\
					_get_extension()->call_virtual_with_data(_get_extension_instance(), &_orblit_virtual_##m_name##_sn, _orblit_virtual_##m_name, $CALLPTRARGPASS, $CALLPTRRETPASS);\\
					$CALLPTRRET\\
				} else {\\
					((NativeExtensionClassCallVirtual)_orblit_virtual_##m_name)(_get_extension_instance(), $CALLPTRARGPASS, $CALLPTRRETPASS);\\
					$CALLPTRRET\\
				}\\
				return true;\\
			}\\
		}\\
		$REQCHECK\\
		$RVOID\\
		return false;\\
	}\\
	_FORCE_INLINE_ bool _orblit_virtual_##m_name##_overridden() const {\\
		static const StringName _orblit_virtual_##m_name##_sn = StringName(#m_name, true);\\
		$SCRIPTHASMETHOD\\
		if (_get_extension()) {\\
			if (unlikely(!_orblit_virtual_##m_name)) {\\
			    _orblit_virtual_init_method_ptr(_orblit_virtual_##m_name##_get_method_info().get_method_hash(), _orblit_virtual_##m_name, _orblit_virtual_##m_name##_sn);\\
			}\\
			if (_orblit_virtual_##m_name != reinterpret_cast<void*>(_INVALID_ORBLIT_VIRTUAL_FUNC_ADDR)) {\\
				return true;\\
			}\\
		}\\
		return false;\\
	}\\
	_FORCE_INLINE_ static MethodInfo _orblit_virtual_##m_name##_get_method_info() {\\
		MethodInfo method_info;\\
		method_info.name = #m_name;\\
		method_info.flags = $METHOD_FLAGS;\\
		$FILL_METHOD_INFO\\
		return method_info;\\
	}

"""


def generate_version(argcount, const=False, returns=False, required=False):
    s = proto
    s = s.replace("$SCRIPTCALL", script_call)
    s = s.replace("$SCRIPTHASMETHOD", script_has_method)

    sproto = str(argcount)
    method_info = ""
    method_flags = "METHOD_FLAG_VIRTUAL"
    if returns:
        sproto += "R"
        s = s.replace("$RET", "m_ret,")
        s = s.replace("$RVOID", "(void)r_ret;")  # If required, may lead to uninitialized errors
        s = s.replace("$CALLPTRRETDEF", "PtrToArg<m_ret>::EncodeT ret;")
        method_info += "method_info.return_val = GetTypeInfo<m_ret>::get_class_info();\\\n"
        method_info += "\t\tmethod_info.return_val_metadata = GetTypeInfo<m_ret>::METADATA;"
    else:
        s = s.replace("$RET ", "")
        s = s.replace("\t\t$RVOID\\\n", "")
        s = s.replace("\t\t\t$CALLPTRRETDEF\\\n", "")

    if const:
        sproto += "C"
        method_flags += " | METHOD_FLAG_CONST"
        s = s.replace("$CONST", "const")
    else:
        s = s.replace("$CONST ", "")

    if required:
        sproto += "_REQUIRED"
        method_flags += " | METHOD_FLAG_VIRTUAL_REQUIRED"
        s = s.replace(
            "$REQCHECK",
            "static bool _orblit_virtual_required_error_shown = false;\\\n\t\t_orblit_virtual_print_required_error(this, _orblit_virtual_##m_name##_sn, _orblit_virtual_required_error_shown);",
        )
    else:
        s = s.replace("\t\t$REQCHECK\\\n", "")

    s = s.replace("$METHOD_FLAGS", method_flags)
    s = s.replace("$VER", sproto)
    argtext = ""
    callargtext = ""
    callsiargs = ""
    callsiargptrs = ""
    callptrargsptr = ""
    if argcount > 0:
        argtext += ", "
        callsiargs = f"Variant vargs[{argcount}] = {{ "
        callsiargptrs = f"\t\t\tconst Variant *vargptrs[{argcount}] = {{ "
        callptrargsptr = f"\t\t\tNativeExtensionConstTypePtr argptrs[{argcount}] = {{ "

        if method_info:
            method_info += "\\\n\t\t"
        method_info += (
            "_orblit_virtual_set_method_info_args<"
            + ", ".join(f"m_type{i + 1}" for i in range(argcount))
            + ">(method_info);"
        )

    callptrargs = ""
    for i in range(argcount):
        if i > 0:
            argtext += ", "
            callargtext += ", "
            callsiargs += ", "
            callsiargptrs += ", "
            callptrargs += "\t\t\t"
            callptrargsptr += ", "
        argtext += f"m_type{i + 1}"
        callargtext += f"m_type{i + 1} arg{i + 1}"
        callsiargs += f"VariantInternal::make(arg{i + 1})"
        callsiargptrs += f"&vargs[{i}]"
        callptrargs += f"PtrToArg<m_type{i + 1}>::EncodeT argval{i + 1}; PtrToArg<m_type{i + 1}>::encode(arg{i + 1}, &argval{i + 1});\\\n"
        callptrargsptr += f"&argval{i + 1}"

    if argcount:
        callsiargs += " };\\\n"
        callsiargptrs += " };"
        s = s.replace("$CALLSIARGS", callsiargs + callsiargptrs)
        s = s.replace("$CALLSIARGPASS", f"(const Variant **)vargptrs, {argcount}")
        callptrargsptr += " };"
        s = s.replace("$CALLPTRARGS", callptrargs + callptrargsptr)
        s = s.replace("$CALLPTRARGPASS", "reinterpret_cast<NativeExtensionConstTypePtr *>(argptrs)")
    else:
        s = s.replace("\t\t\t$CALLSIARGS\\\n", "")
        s = s.replace("$CALLSIARGPASS", "nullptr, 0")
        s = s.replace("\t\t\t$CALLPTRARGS\\\n", "")
        s = s.replace("$CALLPTRARGPASS", "nullptr")

    if returns:
        if argcount > 0:
            callargtext += ", "
        callargtext += "m_ret &r_ret"
        s = s.replace("$CALLSIBEGIN", "Variant ret = ")
        s = s.replace("$CALLSIRET", "r_ret = VariantCaster<m_ret>::cast(ret);")
        s = s.replace("$CALLPTRRETPASS", "&ret")
        s = s.replace("$CALLPTRRET", "r_ret = (m_ret)ret;")
    else:
        s = s.replace("$CALLSIBEGIN", "")
        s = s.replace("\t\t\t\t$CALLSIRET\\\n", "")
        s = s.replace("$CALLPTRRETPASS", "nullptr")
        s = s.replace("\t\t\t\t$CALLPTRRET\\\n", "")

    s = s.replace(" $ARG", argtext)
    s = s.replace("$CALLARGS", callargtext)
    if method_info:
        s = s.replace("$FILL_METHOD_INFO", method_info)
    else:
        s = s.replace("\t\t$FILL_METHOD_INFO\\\n", method_info)

    return s


def run(target, source, env):
    max_versions = 12

    txt = """/* THIS FILE IS GENERATED DO NOT EDIT */
#pragma once

// IWYU pragma: begin_keep
#include "core/object/script_instance.h"
#include "core/variant/method_ptrcall.h"
#include "core/variant/variant_caster.h"
#include "core/variant/variant_internal.h"
// IWYU pragma: end_keep

inline constexpr uintptr_t _INVALID_ORBLIT_VIRTUAL_FUNC_ADDR = static_cast<uintptr_t>(-1);

template <typename... Args>
_NO_INLINE_ void _orblit_virtual_set_method_info_args(MethodInfo &p_method_info) {
	p_method_info.arguments = { GetTypeInfo<Args>::get_class_info()... };
	p_method_info.arguments_metadata = { GetTypeInfo<Args>::METADATA... };
}

_NO_INLINE_ inline void _orblit_virtual_print_required_error(const Object *p_object, const StringName &p_method_name, bool &r_error_shown) {
	// Like ERR_PRINT_ONCE but outlined, to save on binary space.
	if (!r_error_shown) {
		r_error_shown = true;
		ERR_PRINT(vformat("Required virtual method %s::%s must be overridden before calling.", p_object->get_class_name(), p_method_name));
	}
}

"""

    for i in range(max_versions + 1):
        txt += f"/* {i} Arguments */\n\n"
        txt += generate_version(i, False, False)
        txt += generate_version(i, False, True)
        txt += generate_version(i, True, False)
        txt += generate_version(i, True, True)
        txt += generate_version(i, False, False, True)
        txt += generate_version(i, False, True, True)
        txt += generate_version(i, True, False, True)
        txt += generate_version(i, True, True, True)

    with open(str(target[0]), "w", encoding="utf-8", newline="\n") as f:
        f.write(txt)
