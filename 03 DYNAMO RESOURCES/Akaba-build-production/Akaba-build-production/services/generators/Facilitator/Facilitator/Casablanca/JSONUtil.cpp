#include <stdafx.h>

#include <JSONUtil.h>

using namespace web;

bool JSONUtil::extractBool(const json::object& object, string_t tag, bool& value)
{
  const auto& it(object.find(tag));
  if (it == object.end())
    return false;

  if (!it->second.is_null() && it->second.is_boolean())
    value = it->second.as_bool();

  return true;
}

bool JSONUtil::extractInt(const json::object& object, string_t tag, int& value)
{
  const auto& it(object.find(tag));
  if (it == object.end())
    return false;

  if (!it->second.is_null() && it->second.is_number())
    value = static_cast<int>(it->second.as_double());

  return true;
}

bool JSONUtil::extractFloat(const json::object& object, string_t tag, float& value)
{
  const auto& it(object.find(tag));
  if (it == object.end())
    return false;

  if (!it->second.is_null() && it->second.is_number())
    value = static_cast<float>(it->second.as_double());

  return true;
}

bool JSONUtil::extractString(const json::object& object, string_t tag, string_t& value)
{
  const auto& it(object.find(tag));
  if (it == object.end())
    return false;

  if (!it->second.is_null() && it->second.is_string())
    value = it->second.as_string();

  return true;
}

const json::object* JSONUtil::extractObject(const json::object& object, string_t tag)
{
  const auto& it(object.find(tag));
  if (it == object.end())
    return nullptr;

  if (it->second.is_null())
    return nullptr;

  if (!it->second.is_object())
    return nullptr;

  return &it->second.as_object();
}

const json::array* JSONUtil::extractArray(const json::object& object, string_t tag)
{
  const auto& it(object.find(tag));
  if (it == object.end())
    return nullptr;

  if (it->second.is_null())
    return nullptr;

  if (!it->second.is_array())
    return nullptr;

  return &it->second.as_array();
}

////////////////////////////////////////////////////////////////////////////////////////
// Point Helpers

Point3f JSONUtil::Point3fFromJSON(const web::json::value& value)
{
  Point3f result;
  result.x() = static_cast<float>(value.at(0).as_double());
  result.y() = static_cast<float>(value.at(1).as_double());
  result.z() = static_cast<float>(value.at(2).as_double());
  return result;
}

web::json::value JSONUtil::Point3fAsJSON(const Point3f& pt)
{
  web::json::value result = web::json::value::array(3);
  result[0] = web::json::value::number(pt.x());
  result[1] = web::json::value::number(pt.y());
  result[2] = web::json::value::number(pt.z());
  return result;
}
