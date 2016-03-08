#pragma once

#include <GeneratorListener.h>


class ExeBridgeListener : public GeneratorListener
{
public:
  static void start(web::http::uri url);

  ExeBridgeListener(web::http::uri url);

protected:

  unique_ptr<LayoutJob> createJob(const web::json::object& jobData) const;
};
