# config valid only for Capistrano 3.1
#lock '3.2.1'
set :application, 'log_forwarder'
set :repo_url, 'git@github.com:wmaryszczak/log_forwarder.git'

# Default branch is :master
# ask :branch, proc { `git rev-parse --abbrev-ref HEAD`.chomp }.call
set :branch, ENV['BRANCH_NAME'] || 'master'

# Default deploy_to directory is /var/www/my_app
set :deploy_to, "/opt/#{fetch(:application)}"

# Default value for :scm is :git
#set :scm, :git
set :scm, :gitcopy
set :git_strategy, Capistrano::GitCopy::CompileStrategy
set :publish_dir, "#{fetch(:local_path)}/log_forwarder"
set :publish_cmd, "cd #{fetch(:local_path)} && dotnet restore && cd #{fetch(:local_path)} && dotnet publish -f netcoreapp2.1 -c release -o #{fetch(:publish_dir)}"
set :keep_releases, 2

namespace :deploy do  
  namespace :dotnet do

    desc 'Run as a service'
    task :run_service do
      on roles(:app) do
        puts '**** run app as a service'
        if(test("bash -l -c \"pgrep systemd\""))
          if !test("bash -l -c \"sudo systemctl restart #{fetch(:application)}\"")
            execute "bash -l -c \"sudo systemctl start #{fetch(:application)}\""
          end
        else
          if !test("bash -l -c \"sudo initctl restart #{fetch(:application)}\"")
            execute "bash -l -c \"sudo initctl start #{fetch(:application)}\""
          end
        end
      end
    end
    
    desc 'Add to init daemon script'
    task :initd do
      on roles(:app) do
        if(test("bash -l -c \"pgrep systemd\""))
          template "#{fetch(:application)}.service", "#{deploy_to}/current/#{fetch(:application)}.service"
          execute "bash -l -c \"sudo ln -f #{deploy_to}/current/#{fetch(:application)}.service /etc/systemd/system/#{fetch(:application)}.service\""
        else
          template "#{fetch(:application)}.conf", "#{deploy_to}/current/#{fetch(:application)}.conf"
          execute "bash -l -c \"sudo ln -f #{deploy_to}/current/#{fetch(:application)}.conf /etc/init/#{fetch(:application)}.conf\""
        end
      end
    end
  end
  after 'deploy:publishing',                            'deploy:dotnet:initd'
  after 'deploy:finished',                              'deploy:dotnet:run_service'
end